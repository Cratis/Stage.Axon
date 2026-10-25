// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ReadModelRecords
{
    internal static string Code(ViewJavaTypes types, string package, SemanticReadModel model)
    {
        var name = JavaNames.Type(model.Name);
        var components = string.Join(", ", model.Properties.Select(property => $"{JavaRecordComponents.JsonProperty(property.Name)}{types.Type(property.Type)} {JavaNames.Member(property.Name)}"));
        return new JavaCodeBuilder(package).Block($"public record {name}({components})", body =>
        {
            foreach (var property in model.Properties)
            {
                var arguments = model.Properties.Select(candidate => candidate.Id == property.Id ? "value" : JavaNames.Member(candidate.Name) + "()");
                body.Block($"public {name} with{JavaNames.Type(property.Name)}({types.Type(property.Type)} value)", method =>
                    method.Line($"return new {name}({string.Join(", ", arguments)});"));
            }
        }).Build();
    }
}
