// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ReducerModelAvailability
{
    static readonly ConditionalWeakTable<AxonEmissionContext, ReducerModelAvailability> _cache = [];
    readonly Dictionary<SemanticId, string> _unavailable = [];
    readonly Dictionary<SemanticReducer, ReducerBodies.Result> _admissions = [];

    ReducerModelAvailability(AxonEmissionContext context)
    {
        foreach (var owner in ScopeSelection.Select(context.Request))
        {
            foreach (var reducer in owner.Slice.Reducers)
            {
                var admission = ReducerBodies.Admission(context, owner, reducer);
                _admissions.Add(reducer, admission);
                if (Reason(reducer, admission) is { } reason) _unavailable.TryAdd(reducer.ReadModel, reason);
            }
        }
    }

    internal bool Any => _unavailable.Count > 0;

    internal static ReducerModelAvailability For(AxonEmissionContext context) => _cache.GetValue(context, key => new(key));

    internal static string? Reason(SemanticReducer reducer, ReducerBodies.Result admission)
    {
        if (admission.Blocked is { } blocked) return blocked;
        if (reducer.Transitions.IsEmpty) return "The reducer declares no transition body or observed event.";
        return admission.Transitions.Values.FirstOrDefault(transition => !transition.Placed)?.Reason;
    }

    internal string? Reason(SemanticId model) => _unavailable.GetValueOrDefault(model);

    internal ReducerBodies.Result Admission(SemanticReducer reducer) => _admissions[reducer];
}
