// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ReadModelWriters
{
    internal static bool Writerless(AxonEmissionContext context, SemanticId model)
    {
        var owners = ScopeSelection.Select(context.Request);
        return !owners.SelectMany(owner => owner.Slice.Projections).Any(projection => projection.ReadModel == model)
            && !owners.SelectMany(owner => owner.Slice.Reducers).Any(reducer => reducer.ReadModel == model);
    }
}
