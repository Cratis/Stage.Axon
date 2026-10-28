// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
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
        var code = new JavaCodeBuilder(AxonRuntimePackage.InlinePackage(context)).Declare("UnsupportedImplementations");
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
        context.AddJava(AxonRuntimePackage.InlinePackage(context), "UnsupportedImplementations", code.Build(), sources: [.. pending.Select(requirement => requirement.Source.SemanticId).Distinct()]);
    }

    static string Reason(AxonEmissionContext context, SemanticImplementationRequirement requirement)
    {
        var slot = ReactionSlot(context, requirement) ?? new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId);
        return InlineJavaBodies.TryResolve(context, slot, out _, out var reason)
            ? "No supported native method-body slot consumed this implementation."
            : reason;
    }

    static InlineBodySlot? ReactionSlot(AxonEmissionContext context, SemanticImplementationRequirement requirement)
    {
        if (requirement.Role != SemanticImplementationRole.ReactionEffect) return null;
        foreach (var located in ScopeSelection.Locate(context.Request.Model.Application))
        {
            foreach (var reaction in located.Slice.Reactions)
            {
                var slot = InlineBodySlot.ForReaction(requirement.RequirementId, reaction, located.Slice.Id);
                if (reaction.Triggers.Any(trigger => trigger.RequirementId == requirement.RequirementId) && slot.Addresses(requirement)) return slot;
            }
        }

        return null;
    }

    static bool Selected(SemanticImplementationRequirement requirement, LocatedSemanticSlice located) => requirement.Role switch
    {
        SemanticImplementationRole.CommandHandler => CommandSelected(requirement, located),
        SemanticImplementationRole.ReactionEffect => ReactionSelected(requirement, located),
        SemanticImplementationRole.QueryPerformer => QuerySelected(requirement, located),
        SemanticImplementationRole.ReducerTransition => ReducerSelected(requirement, located),
        _ => false
    };

    static bool ReducerSelected(SemanticImplementationRequirement requirement, LocatedSemanticSlice located) =>
        located.Slice.Reducers.Any(reducer => reducer.Transitions.Any(transition => transition.RequirementId == requirement.RequirementId));

    static bool CommandSelected(SemanticImplementationRequirement requirement, LocatedSemanticSlice located) =>
        located.Slice.Commands.Any(command => command.Id == requirement.Source.SemanticId && command.Handler?.RequirementId == requirement.RequirementId);

    static bool ReactionSelected(SemanticImplementationRequirement requirement, LocatedSemanticSlice located) =>
        located.Slice.Reactions.Any(reaction => reaction.Triggers.Any(trigger => trigger.RequirementId == requirement.RequirementId) &&
            InlineBodySlot.ForReaction(requirement.RequirementId, reaction, located.Slice.Id).Addresses(requirement));

    static bool QuerySelected(SemanticImplementationRequirement requirement, LocatedSemanticSlice located) =>
        located.Slice.Queries.Any(query => query.Id == requirement.Source.SemanticId) || located.Slice.ReadModels.Any(model => model.Id == requirement.QueryShape?.ReadModel);
}
