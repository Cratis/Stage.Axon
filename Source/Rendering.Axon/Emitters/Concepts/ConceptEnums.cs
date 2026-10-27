// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class ConceptEnums
{
    /// <summary>Whether the enumeration is written on the wire as numbers; this is the single predicate the Java and TypeScript renderings share.</summary>
    /// <param name="concept">The enumeration concept.</param>
    /// <returns>True when every value has a number.</returns>
    internal static bool IsNumeric(SemanticConcept concept) => concept.Numbers is { } numbers && numbers.Length == concept.Values.Length;

    internal static string Emit(AxonEmissionContext context, SemanticConcept concept)
    {
        var name = JavaNames.Type(concept.Name);
        var numeric = IsNumeric(concept);
        var code = JavaSemanticTypes.ConceptBuilder(context, name);
        var primitive = code.Reference(numeric ? "java.lang.Integer" : "java.lang.String");
        code.Block("public enum " + name, body =>
            {
                for (var index = 0; index < concept.Values.Length; index++)
                {
                    var value = numeric ? concept.Numbers!.Value[index].ToString(System.Globalization.CultureInfo.InvariantCulture) : JavaCodeBuilder.StringLiteral(concept.Values[index]);
                    body.Line(JavaNames.Member(concept.Values[index]).ToUpperInvariant() + "(" + value + ")" + (index == concept.Values.Length - 1 ? ";" : ","));
                }
                body.Line($"private final {primitive} value;")
                    .Line($"{name}({primitive} value) {{ this.value = value; }}")
                    .Line("@" + body.Reference("com.fasterxml.jackson.annotation.JsonValue"))
                    .Line($"public {primitive} value() {{ return value; }}")
                    .Line("@" + body.Reference("java.lang.Override"))
                    .Line($"public {body.Reference("java.lang.String")} toString() {{ return value.toString(); }}")
                    .Line("@" + body.Reference("com.fasterxml.jackson.annotation.JsonCreator"))
                    .Block($"public static {name} fromValue({primitive} value)", method => method
                        .Line("for (var candidate : values()) if (candidate.value.equals(value)) return candidate;")
                        .Line($"throw new {method.Reference("java.lang.IllegalArgumentException")}(\"Unknown " + name + " value: \" + value);"));
            });
        return code.Build();
    }
}
