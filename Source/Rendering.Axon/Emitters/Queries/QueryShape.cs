// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal sealed class QueryShape(SemanticKeyedQuery query, SemanticReadModel model, ViewJavaTypes types, SemanticImplementationRequirement? performer = null)
{
    static readonly (string Qualified, string Token, string Suffix)[] _parsingTypes =
    [
        ("java.util.UUID", "java.util.UUID", string.Empty),
        ("java.math.BigDecimal", "java.math.BigDecimal", string.Empty),
        ("java.time.LocalDate", "java.time.LocalDate", string.Empty),
        ("java.time.OffsetDateTime", "java.time.OffsetDateTime", string.Empty),
        ("java.lang.Long", "Long.valueOf", ".valueOf"),
        ("java.lang.Boolean", "Boolean.valueOf", ".valueOf")
    ];

    internal IEnumerable<(string Name, SemanticTypeReference Type, SemanticId Property)> Arguments
    {
        get
        {
            if (performer?.QueryShape is { } performed)
            {
                foreach (var parameter in performed.Arguments)
                    yield return (parameter.Name, parameter.Type! with { IsOptional = parameter.IsOptional }, default);
                yield break;
            }

            foreach (var part in query.KeyParts) yield return (part.Name, part.Type, part.KeyProperty);
            if (query.KeyParts.Length == 0 && query.Argument is { } argument && query.KeyProperty is { } key)
                yield return (argument.Name, argument.Type, key);
            foreach (var filter in query.Filters) yield return (filter.Name, filter.Type, filter.Property);
        }
    }

    internal void Claim(JavaCodeBuilder code)
    {
        foreach (var argument in Arguments)
        {
            if (argument.Type.Kind == SemanticTypeReferenceKind.Primitive) continue;
            code.Import(types.Type(argument.Type with { IsCollection = false }));
        }
    }

    internal string Components(JavaCodeBuilder code) => string.Join(", ", Arguments.Select(argument => $"{Type(code, argument.Type)} {JavaNames.Member(argument.Name)}"));

    internal string Parameters(JavaCodeBuilder code) => string.Join(", ", Arguments.Select(argument => $"@{code.Reference("org.springframework.web.bind.annotation.RequestParam")}(name = {JavaCodeBuilder.StringLiteral(argument.Name)}, required = {(argument.Type.IsOptional ? "false" : "true")}) {code.Reference("java.lang.String")} {JavaNames.Member(argument.Name)}"));

    internal string Parsed(JavaCodeBuilder code) => string.Join(", ", Arguments.Select(argument => argument.Type.IsOptional
        ? $"{JavaNames.Member(argument.Name)} == null ? null : {Parse(code, argument.Type, JavaNames.Member(argument.Name))}"
        : Parse(code, argument.Type, JavaNames.Member(argument.Name))));

    internal string Predicate(JavaCodeBuilder code) => string.Join(" && ", Arguments.Select(argument =>
    {
        var property = model.Properties.Single(candidate => candidate.Id == argument.Property);
        var supplied = $"query.{JavaNames.Member(argument.Name)}()";
        var actual = $"row.{JavaNames.Member(property.Name)}()";
        var equality = types.PrimitiveOf(property.Type) == SemanticPrimitiveType.DecimalNumber
            ? $"({actual} != null && {types.Unwrap(property.Type, actual)}.compareTo({types.Unwrap(argument.Type, supplied)}) == 0)"
            : $"{code.Reference("java.util.Objects")}.equals({actual}, {supplied})";
        return argument.Type.IsOptional ? $"({supplied} == null || {equality})" : equality;
    }));

    string Type(JavaCodeBuilder code, SemanticTypeReference type)
    {
        var name = types.Type(type with { IsCollection = false });
        var scalar = code.Reference(name.Contains('.') ? name : "java.lang." + name);
        return type.IsCollection ? $"{code.Reference("java.util.List")}<{scalar}>" : scalar;
    }

    string Parse(JavaCodeBuilder code, SemanticTypeReference type, string text)
    {
        var primitive = type with { Kind = SemanticTypeReferenceKind.Primitive, Primitive = types.PrimitiveOf(type) };
        var value = types.Parse(primitive, text);
        foreach (var parser in _parsingTypes)
        {
            if (!value.Contains(parser.Token, StringComparison.Ordinal)) continue;
            value = value.Replace(parser.Token, code.Reference(parser.Qualified) + parser.Suffix, StringComparison.Ordinal);
        }
        return type.Kind == SemanticTypeReferenceKind.Concept
            ? types.Wrap(type, value).Replace(types.Type(type), Type(code, type), StringComparison.Ordinal)
            : value;
    }
}
