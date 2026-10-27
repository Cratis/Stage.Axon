// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Reports behavioral members no emitter reads (captures, translations, public events, refusal branches) so none is dropped silently.</summary>
internal sealed class AxonBehaviorAdmission : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var group in Classify(context.Request).GroupBy(decision => decision.Package))
        {
            var package = group.Key;
            var builder = new JavaCodeBuilder(package);
            foreach (var decision in group)
            {
                context.Degradation.Unsupported(decision.Code, decision.Owner, decision.Reason, decision.Intent, decision.ModelPath);
                builder.UnsupportedIntent(decision.Intent, decision.ModelPath, decision.Code);
            }

            context.AddJava(package, "package-info", builder.Build(), false, [.. group.Select(decision => decision.Owner).Distinct()]);
        }
    }

    internal static bool DeclaresRefusalBranches(SemanticInvocation invocation) => !invocation.OnRefused.IsDefaultOrEmpty;

    internal static IReadOnlyList<AxonBehaviorDecision> Classify(ArtifactRenderRequest request) =>
        [.. ConceptDecisions(request.Model.Application).Concat(ScopeSelection.Select(request).SelectMany(located => SliceDecisions(Package(request, located), located.Slice)))
            .DistinctBy(decision => (decision.Code, decision.Owner, decision.ModelPath))];

    internal static IEnumerable<AxonBehaviorDecision> ConceptDecisions(SemanticApplication application)
    {
        var package = JavaNames.PackageSegment(application.Name) + ".concepts";
        foreach (var concept in application.Concepts.Where(concept => !concept.Values.IsDefaultOrEmpty && concept.Compliance is { Personal: not null } or { Secret: not null }))
        {
            yield return new(
                AxonRefusalCodes.ConceptCompliance,
                package,
                concept.Id,
                $"{concept.Id}/compliance",
                "protect personal or secret concept values",
                "Enum concepts do not apply protection at rest; no protection semantics are rendered for this concept.");
        }
    }

    internal static IEnumerable<AxonBehaviorDecision> SliceDecisions(string package, SemanticSlice slice)
    {
        foreach (var capture in slice.Captures)
        {
            yield return Decision(package, capture.Id, $"{capture.Id}/capture", $"capture {capture.Name}", "Captures are not rendered; no source subscription or checkpoint is generated.");
        }

        if (slice.Kind == SemanticSliceKind.Translate && slice.Captures.IsDefaultOrEmpty)
        {
            var direction = slice.Direction == SemanticTranslationDirection.Outbound ? "outbound" : "inbound";
            yield return Decision(package, slice.Id, $"{slice.Id}/translation", $"translate {direction} in slice {slice.Name}", "Translations are not rendered.");
        }

        foreach (var contract in slice.Events.Where(contract => contract.Visibility == SemanticEventVisibility.Public))
        {
            yield return Decision(package, contract.Id, $"{contract.Id}/public", $"publish the public event contract {contract.Name}", "Public event contracts are not published; the event is rendered as a private Axon event.");
        }

        foreach (var decision in slice.Reactions.SelectMany(reaction => RefusalBranches(package, reaction)))
        {
            yield return decision;
        }
    }

    static IEnumerable<AxonBehaviorDecision> RefusalBranches(string package, SemanticReaction reaction)
    {
        for (var trigger = 0; trigger < reaction.Triggers.Length; trigger++)
        {
            var invocations = reaction.Triggers[trigger].Invokes;
            for (var invocation = 0; invocation < invocations.Length; invocation++)
            {
                if (!DeclaresRefusalBranches(invocations[invocation])) continue;
                for (var branch = 0; branch < invocations[invocation].OnRefused.Length; branch++)
                {
                    var selector = invocations[invocation].OnRefused[branch].Selector;
                    yield return new(
                        AxonRefusalCodes.RefusalBranch,
                        package,
                        reaction.Id,
                        $"{reaction.Id}/triggers/{trigger}/invokes/{invocation}/on-refused/{branch}",
                        $"handle a refused invocation ({selector}) in reaction {reaction.Name}",
                        "Refusal branches are not rendered.");
                }
            }
        }
    }

    static string Package(ArtifactRenderRequest request, LocatedSemanticSlice located) =>
        JavaNames.PackageSegment(request.Model.Application.Name) + "." + JavaNames.PackageSegment(located.Module.Name);

    static AxonBehaviorDecision Decision(string package, SemanticId owner, string modelPath, string intent, string reason) =>
        new(AxonRefusalCodes.TranslationCapture, package, owner, modelPath, intent, reason);
}

internal sealed record AxonBehaviorDecision(string Code, string Package, SemanticId Owner, string ModelPath, string Intent, string Reason);
