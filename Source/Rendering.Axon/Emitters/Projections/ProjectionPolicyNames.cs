// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionPolicyNames
{
    readonly Dictionary<SemanticId, string> _names = [];

    internal ProjectionPolicyNames(SemanticApplication application, IEnumerable<SemanticId> events)
    {
        var slices = ScopeSelection.Locate(application).Select(owner => owner.Slice).ToArray();
        var contracts = slices.SelectMany(slice => slice.Events).ToDictionary(contract => contract.Id);

        // Reserve application types even when a colliding reference will be qualified. Nested types must not shadow them.
        var reserved = application.Concepts.Select(concept => JavaNames.Type(concept.Name))
            .Concat(application.Types.Select(type => JavaNames.Type(type.Name)))
            .Concat(contracts.Values.Select(contract => JavaNames.Type(contract.Name)))
            .Concat(slices.SelectMany(slice => slice.ReadModels).SelectMany(model => new[] { JavaNames.Type(model.Name), JavaNames.Type(model.Name) + "Reducer", JavaNames.Type(model.Name) + "ReducerProjector" }))
            .Concat(slices.SelectMany(slice => slice.Projections).Select(projection => JavaNames.Type(projection.Name) + "Projector"))
            .Concat(["ScopeChange", "ScopeChangeResult", "ScopeState", "ScopeStateSnapshot", "RollbackWitness", "RejectingDelivery"])
            .ToHashSet(StringComparer.Ordinal);

        // The full contract identity, not its imported/simple Java reference, determines allocation order and lookup.
        foreach (var id in events.Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal))
        {
            var preferred = JavaNames.Type(contracts[id].Name) + "Sequence";
            var name = preferred;
            var suffix = 2;
            while (!reserved.Add(name)) name = preferred + suffix++.ToString(CultureInfo.InvariantCulture);
            _names.Add(id, name);
        }
    }

    internal string For(SemanticId contract) => _names[contract];

    internal void Declare(JavaCodeBuilder code)
    {
        foreach (var name in _names.Values) code.Declare(name);
    }
}
