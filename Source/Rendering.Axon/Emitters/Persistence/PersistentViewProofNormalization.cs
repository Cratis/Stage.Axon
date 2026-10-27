// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewProofNormalization
{
    internal static string Body(SemanticApplication application, ViewJavaTypes types, SemanticReadModel model, IEnumerable<SemanticProjection> projections)
    {
        var records = new ProjectionRecords(application, types);
        var mappings = projections.SelectMany(ProjectionTransitions.From).SelectMany(from => from.Mappings)
            .Where(mapping => mapping.Source is SemanticProjectionEventContextValue || mapping.Factor is SemanticProjectionEventContextValue);
        var lines = mappings.Select(mapping => "row = " + records.Set(JavaNames.Type(model.Name), model.Properties, mapping.Target, "row", "null") + ";");
        return string.Join("\n        ", lines.Append("return row;"));
    }
}
