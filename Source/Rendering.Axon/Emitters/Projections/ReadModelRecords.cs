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
        var code = new JavaCodeBuilder(package).Declare(name);
        foreach (var property in model.Properties) types.Claim(property.Type, code);
        var components = string.Join(", ", model.Properties.Select(property => Component(types, property, code)));
        return code.Block($"public record {name}({components})", body =>
        {
            foreach (var property in model.Properties)
            {
                var arguments = model.Properties.Select(candidate => candidate.Id == property.Id ? "value" : JavaNames.Member(candidate.Name) + "()");
                body.Block($"public {name} with{JavaNames.Type(property.Name)}({types.Type(property.Type, body)} value)", method =>
                    method.Line($"return new {name}({string.Join(", ", arguments)});"));
            }
        }).Build();
    }

    static string Component(ViewJavaTypes types, SemanticProperty property, JavaCodeBuilder code)
    {
        var name = JavaNames.Member(property.Name);
        var json = JavaJsonNames.Annotation(property.Name, code);
        return $"{json}{types.Type(property.Type, code)} {name}";
    }
}
