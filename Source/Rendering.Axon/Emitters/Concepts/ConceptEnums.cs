// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class ConceptEnums
{
    internal static string Emit(AxonEmissionContext context, SemanticConcept concept)
    {
        var name = JavaNames.Type(concept.Name);
        var numeric = concept.Numbers is { } numbers && numbers.Length == concept.Values.Length;
        var primitive = numeric ? "Integer" : "String";
        var code = new JavaCodeBuilder(context.BasePackage + ".concepts")
            .Import("com.fasterxml.jackson.annotation.JsonCreator")
            .Import("com.fasterxml.jackson.annotation.JsonValue")
            .Block("public enum " + name, body =>
            {
                for (var index = 0; index < concept.Values.Length; index++)
                {
                    var value = numeric ? concept.Numbers!.Value[index].ToString(System.Globalization.CultureInfo.InvariantCulture) : JavaCodeBuilder.StringLiteral(concept.Values[index]);
                    body.Line(JavaNames.Member(concept.Values[index]).ToUpperInvariant() + "(" + value + ")" + (index == concept.Values.Length - 1 ? ";" : ","));
                }
                body.Line($"private final {primitive} value;")
                    .Line($"{name}({primitive} value) {{ this.value = value; }}")
                    .Line("@JsonValue")
                    .Line($"public {primitive} value() {{ return value; }}")
                    .Line("@JsonCreator")
                    .Block($"public static {name} fromValue({primitive} value)", method => method
                        .Line("for (var candidate : values()) if (candidate.value.equals(value)) return candidate;")
                        .Line("throw new IllegalArgumentException(\"Unknown " + name + " value: \" + value);"));
            });
        return code.Build();
    }
}
