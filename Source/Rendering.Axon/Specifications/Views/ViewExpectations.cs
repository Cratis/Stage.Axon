// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class ViewExpectations(SemanticApplication application, ViewJavaTypes types)
{
    internal string Predicate(SemanticValue expected, SemanticTypeReference type, string actual) => expected switch
    {
        SemanticNullValue => actual + " == null",
        SemanticCompositeValue composite => Composite(composite, type, actual),
        SemanticArrayValue array => Array(array, type, actual),
        _ => Scalar(expected, type, actual)
    };

    string Scalar(SemanticValue expected, SemanticTypeReference type, string actual)
    {
        var value = types.Literal(expected, type);
        if (types.PrimitiveOf(type) == SemanticPrimitiveType.DecimalNumber)
            return $"({actual} != null && {types.Unwrap(type, actual)}.compareTo({types.Unwrap(type, value)}) == 0)";
        return $"java.util.Objects.equals({actual}, {value})";
    }

    string Composite(SemanticCompositeValue expected, SemanticTypeReference type, string actual)
    {
        var composite = application.Types.First(candidate => candidate.Id == type.Target);
        var properties = expected.Properties.Select(value =>
        {
            var property = composite.Properties.First(candidate => candidate.Id == value.TargetProperty);
            return Predicate(value.Value, property.Type, actual + "." + JavaNames.Member(property.Name) + "()");
        });
        return $"({actual} != null && {string.Join(" && ", properties.DefaultIfEmpty("true"))})";
    }

    string Array(SemanticArrayValue expected, SemanticTypeReference type, string actual)
    {
        var items = expected.Values.Select((value, index) => Predicate(value, type with { IsCollection = false, IsOptional = false }, $"{actual}.get({index})"));
        return $"({actual} != null && {actual}.size() == {expected.Values.Length} && {string.Join(" && ", items.DefaultIfEmpty("true"))})";
    }
}
