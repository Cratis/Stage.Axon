// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Reports behavioral members no emitter reads (captures, translations, public events, refusal branches, routing, unplaced roles) so none is dropped silently. Every report is a pure function of the model and the selected scope, so the walk runs before all emitters and later emitters can see its diagnostics.</summary>
internal sealed class AxonBehaviorAdmission : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context) => Apply(context, Classify(context.Request));

    internal static void Apply(AxonEmissionContext context, IEnumerable<AxonBehaviorDecision> decisions)
    {
        foreach (var group in decisions.GroupBy(decision => decision.Package))
        {
            var package = group.Key;
            var builder = new JavaCodeBuilder(package);
            var reportable = group.ToArray();
            foreach (var decision in reportable)
            {
                context.Degradation.Unsupported(decision.Code, decision.Owner, decision.Reason, decision.Intent, decision.ModelPath);
                builder.UnsupportedIntent(decision.Intent, decision.ModelPath, decision.Code);
            }

            context.AddJava(package, "package-info", builder.Build(), false, [.. reportable.Select(decision => decision.Owner).Distinct()]);
        }
    }

    internal static bool DeclaresRefusalBranches(SemanticInvocation invocation) => !invocation.OnRefused.IsDefaultOrEmpty;

    internal static IReadOnlyList<AxonBehaviorDecision> Classify(ArtifactRenderRequest request) =>
        [.. InScope(request, ConceptDecisions(request.Model.Application, AxonNamesInput.BasePackage(request)).Concat(ImplementationDecisions(request)).Concat(ScopeSelection.Select(request).SelectMany(located => SliceDecisions(Package(request, located), located.Slice))))
            .DistinctBy(decision => (decision.Code, decision.Owner, decision.ModelPath))];

    internal static IEnumerable<AxonBehaviorDecision> ConceptDecisions(SemanticApplication application, string basePackage)
    {
        var package = basePackage + ".concepts";
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

        foreach (var command in slice.Commands.Where(RoutedWithoutAppend))
        {
            yield return new(
                AxonRefusalCodes.Routing,
                package,
                command.Id,
                $"{command.Id}/produces",
                "route events through modeled source and substream partitions",
                "Only the primary event-source tag is used; source/substream routing metadata is not represented.");
        }

        foreach (var decision in slice.Reactions.SelectMany(reaction => RefusalBranches(package, reaction)))
        {
            yield return decision;
        }
    }

    // A native handler (or a command that appends nothing) never reaches CommandHandlerEmitter.MetadataDifferences, so its routing would vanish.
    static bool RoutedWithoutAppend(SemanticCommand command) =>
        (command.Route is not null || command.Produces.Any(produced => produced.Route is not null)) && (command.Handler is not null || command.Produces.IsDefaultOrEmpty);

    // Requirement roles no inline-body slot selects are never reported by InlineImplementationDiagnostics.
    static IEnumerable<AxonBehaviorDecision> ImplementationDecisions(ArtifactRenderRequest request)
    {
        var package = AxonRuntimePackage.InlinePackage(AxonNamesInput.BasePackage(request));
        var requirements = request.ImplementationRequirements.IsDefault ? [] : request.ImplementationRequirements;
        foreach (var requirement in requirements.Where(requirement => requirement.Role is not (SemanticImplementationRole.CommandHandler or SemanticImplementationRole.ReactionEffect or SemanticImplementationRole.QueryPerformer or SemanticImplementationRole.ReducerTransition)))
        {
            yield return new(
                AxonRefusalCodes.Implementation,
                package,
                requirement.Source.SemanticId,
                string.Join('/', requirement.Owner.Parts.Select(part => part.Key)) + "/" + requirement.RequirementId,
                $"execute {requirement.Role} {requirement.RequirementId}",
                "No Axon emitter places native " + requirement.Role + " implementations.");
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
        AxonNamesInput.BasePackage(request) + "." + JavaNames.PackageSegment(located.Module.Name);

    static AxonBehaviorDecision Decision(string package, SemanticId owner, string modelPath, string intent, string reason) =>
        new(AxonRefusalCodes.TranslationCapture, package, owner, modelPath, intent, reason);

    // One scope predicate for every family: an owner is reported only if it lives in a selected slice or is an application-level concept (which the concepts emitter renders whatever the scope).
    static IEnumerable<AxonBehaviorDecision> InScope(ArtifactRenderRequest request, IEnumerable<AxonBehaviorDecision> decisions)
    {
        var owners = new HashSet<SemanticId>(request.Model.Application.Concepts.Select(concept => concept.Id));
        foreach (var located in ScopeSelection.Select(request)) Collect(located.Slice, owners, []);
        return decisions.Where(decision => owners.Contains(decision.Owner));
    }

    // A default (uninitialized) ImmutableArray throws when enumerated; treat it as empty.
    static List<object?> Items(System.Collections.IEnumerable items)
    {
        var list = new List<object?>();
        try
        {
            foreach (var item in items) list.Add(item);
        }
        catch (InvalidOperationException)
        {
            list.Clear();
        }

        return list;
    }

    static void Collect(object? node, HashSet<SemanticId> ids, HashSet<object> seen)
    {
        switch (node)
        {
            case null or string or Enum:
                return;
            case { } primitive when primitive.GetType().IsPrimitive:
                return;
            case SemanticId id:
                ids.Add(id);
                return;
            case System.Collections.IEnumerable items:
                foreach (var item in Items(items)) Collect(item, ids, seen);
                return;
            default:
                if (!seen.Add(node)) return;
                foreach (var property in node.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0)) Collect(property.GetValue(node), ids, seen);
                return;
        }
    }
}

internal sealed record AxonBehaviorDecision(string Code, string Package, SemanticId Owner, string ModelPath, string Intent, string Reason);
