// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations;

internal static class InlineImplementationDiagnostics
{
    internal static void Emit(AxonEmissionContext context)
    {
        var selected = ScopeSelection.Select(context.Request);
        var pending = InlineJavaBodies.Requirements(context)
            .Where(requirement => !context.ConsumedImplementations.Contains(requirement.RequirementId) && selected.Any(slice => Selected(requirement, slice)))
            .OrderBy(requirement => requirement.RequirementId, StringComparer.Ordinal).ToArray();
        if (pending.Length == 0) return;
        var code = new JavaCodeBuilder(context.BasePackage + ".runtime").Declare("UnsupportedImplementations");
        code.Block("public final class UnsupportedImplementations", body =>
        {
            body.Line("private UnsupportedImplementations() {}");
            foreach (var requirement in pending)
            {
                var refusal = requirement.Language is not null and not "java" ? AxonRefusalCodes.ImplementationLanguage : AxonRefusalCodes.Implementation;
                var reason = Reason(context, requirement);
                var path = string.Join('/', requirement.Owner.Parts.Select(part => part.Key)) + "/" + requirement.RequirementId;
                context.Degradation.Unsupported(refusal, requirement.Source.SemanticId, reason, $"execute {requirement.Role} {requirement.RequirementId}", path);
                body.UnsupportedIntent($"execute {requirement.Role}: {reason}", path, refusal);
            }
        });
        context.AddJava(context.BasePackage + ".runtime", "UnsupportedImplementations", code.Build(), sources: [.. pending.Select(requirement => requirement.Source.SemanticId).Distinct()]);
    }

    static string Reason(AxonEmissionContext context, SemanticImplementationRequirement requirement)
    {
        if (requirement.Role == SemanticImplementationRole.ReducerTransition) return "Reducer transition bodies have no supported Axon method-body slot.";
        return InlineJavaBodies.TryResolve(context, requirement.RequirementId, requirement.Role, requirement.Source.SemanticId, out _, out var reason)
            ? "No supported native method-body slot consumed this implementation."
            : reason;
    }

    static bool Selected(SemanticImplementationRequirement requirement, LocatedSemanticSlice located) => requirement.Role switch
    {
        SemanticImplementationRole.CommandHandler => located.Slice.Commands.Any(command => command.Id == requirement.Source.SemanticId && command.Handler?.RequirementId == requirement.RequirementId),
        SemanticImplementationRole.ReactionEffect => located.Slice.Reactions.Any(reaction => reaction.Id == requirement.Source.SemanticId && reaction.Triggers.Any(trigger => trigger.RequirementId == requirement.RequirementId)),
        SemanticImplementationRole.QueryPerformer => located.Slice.Queries.Any(query => query.Id == requirement.Source.SemanticId) || located.Slice.ReadModels.Any(model => model.Id == requirement.QueryShape?.ReadModel),
        SemanticImplementationRole.ReducerTransition => located.Slice.Reducers.Any(reducer => reducer.Transitions.Any(transition => transition.RequirementId == requirement.RequirementId)),
        _ => false
    };
}
