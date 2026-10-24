// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal sealed class QueryShape(SemanticKeyedQuery query, SemanticReadModel model, ViewJavaTypes types)
{
    internal IEnumerable<(string Name, SemanticTypeReference Type, SemanticId Property)> Arguments
    {
        get
        {
            foreach (var part in query.KeyParts) yield return (part.Name, part.Type, part.KeyProperty);
            if (query.KeyParts.Length == 0 && query.Argument is { } argument && query.KeyProperty is { } key)
                yield return (argument.Name, argument.Type, key);
            foreach (var filter in query.Filters) yield return (filter.Name, filter.Type, filter.Property);
        }
    }

    internal string Components => string.Join(", ", Arguments.Select(argument => $"{types.Type(argument.Type)} {JavaNames.Member(argument.Name)}"));

    internal string Parameters => string.Join(", ", Arguments.Select(argument => $"@RequestParam(name = {JavaCodeBuilder.StringLiteral(argument.Name)}, required = {(argument.Type.IsOptional ? "false" : "true")}) String {JavaNames.Member(argument.Name)}"));

    internal string Parsed => string.Join(", ", Arguments.Select(argument => argument.Type.IsOptional
        ? $"{JavaNames.Member(argument.Name)} == null ? null : {types.Parse(argument.Type, JavaNames.Member(argument.Name))}"
        : types.Parse(argument.Type, JavaNames.Member(argument.Name))));

    internal string Predicate => string.Join(" && ", Arguments.Select(argument =>
    {
        var property = model.Properties.Single(candidate => candidate.Id == argument.Property);
        var supplied = $"query.{JavaNames.Member(argument.Name)}()";
        var actual = $"row.{JavaNames.Member(property.Name)}()";
        var equality = types.PrimitiveOf(property.Type) == SemanticPrimitiveType.DecimalNumber
            ? $"({actual} != null && {types.Unwrap(property.Type, actual)}.compareTo({types.Unwrap(argument.Type, supplied)}) == 0)"
            : $"java.util.Objects.equals({actual}, {supplied})";
        return argument.Type.IsOptional ? $"({supplied} == null || {equality})" : equality;
    }));
}
