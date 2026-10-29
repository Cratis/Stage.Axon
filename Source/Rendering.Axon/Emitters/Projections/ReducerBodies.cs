// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ReducerBodies
{
    internal static Result Admission(AxonEmissionContext context, LocatedSemanticSlice located, SemanticReducer reducer)
    {
        var application = context.Request.Model.Application;
        var model = located.Slice.ReadModels.FirstOrDefault(candidate => candidate.Id == reducer.ReadModel);
        var events = ScopeSelection.Locate(application).SelectMany(slice => slice.Slice.Events).ToArray();
        (bool Invalid, string Reason, string Code)[] checks =
        [
            (reducer.Target != SemanticProjectionTargetKind.ReadModel, "The reducer targets an event rather than a read model.", AxonRefusalCodes.Reduction),
            (model is null, "The reducer read model is outside its slice.", AxonRefusalCodes.Reduction),
            (model is not null && ReadModelKeys.IsComposite(application, model), "The reducer uses an event-source String key, but its read model requires a composite embedded id.", AxonRefusalCodes.CompositeKey),
            (reducer.Key != SemanticReducerKey.EventSourceId, "The reducer key is unsupported.", AxonRefusalCodes.Reduction),
            (reducer.Result != SemanticReducerResult.StateOrDelete, "The reducer result is unsupported.", AxonRefusalCodes.Reduction),
            (reducer.Transitions.GroupBy(transition => transition.EventContract).Any(group => group.Count() > 1), "The reducer has duplicate event transitions.", AxonRefusalCodes.Reduction),
            (reducer.Transitions.Any(transition => events.Count(candidate => candidate.Id == transition.EventContract) != 1), "A reducer event contract is missing or ambiguous.", AxonRefusalCodes.Reduction)
        ];
        var blocked = checks.FirstOrDefault(check => check.Invalid);
        var transitions = new Dictionary<string, Transition>(StringComparer.Ordinal);
        foreach (var transition in reducer.Transitions)
        {
            var reason = blocked.Reason ?? (reducer.From is not null ? "The reducer's event filter cannot be applied by this projector." : string.Empty);
            var body = string.Empty;
            var placed = reason.Length == 0 && InlineJavaBodies.TryResolve(context, new(transition.RequirementId, SemanticImplementationRole.ReducerTransition, located.Slice.Id), out body, out reason);
            transitions[transition.RequirementId] = new(placed, body, reason);
        }
        return new(blocked.Reason, transitions, blocked.Code ?? AxonRefusalCodes.Reduction);
    }

    internal sealed record Transition(bool Placed, string Body, string Reason);
    internal sealed record Result(string? Blocked, IReadOnlyDictionary<string, Transition> Transitions, string BlockedCode = AxonRefusalCodes.Reduction);
}
