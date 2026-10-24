// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionValues(SemanticApplication application, ViewJavaTypes types)
{
    internal string? Value(SemanticProjectionValue? value, SemanticTypeReference target, SemanticEventContract @event) => value switch
    {
        SemanticProjectionEventProperty property => EventProperty(property, target, @event),
        SemanticProjectionEventSourceIdentity => types.Parse(target, "event.eventSourceId().value()"),
        SemanticProjectionLiteral literal when literal.Value is SemanticTextValue or SemanticNumberValue or SemanticBooleanValue or SemanticNullValue => types.Literal(literal.Value, target),
        _ => null
    };

    internal string? Key(SemanticProjectionKey key, SemanticEventContract @event) => key switch
    {
        SemanticProjectionValueKey { Value: SemanticProjectionEventSourceIdentity } => "event.eventSourceId().value()",
        SemanticProjectionValueKey { Value: SemanticProjectionLiteral { Value: SemanticTextValue literal } } => JavaCodeBuilder.StringLiteral(literal.Value),
        SemanticProjectionValueKey { Value: SemanticProjectionEventProperty property } => EventKey(property, @event),
        _ => null
    };

    internal string? Mapping(SemanticProjectionMapping mapping, SemanticProperty target, SemanticEventContract @event)
    {
        var old = $"state.{JavaNames.Member(target.Name)}()";
        if (mapping.Operation == SemanticProjectionOperation.Clear) return "null";
        var source = Value(mapping.Source, target.Type, @event);
        if (mapping.Operation == SemanticProjectionOperation.Set) return source;
        var primitive = types.PrimitiveOf(target.Type);
        if (primitive is not (SemanticPrimitiveType.WholeNumber or SemanticPrimitiveType.DecimalNumber)) return null;
        var left = $"({old} == null ? {types.Default(target.Type)} : {old})";
        left = types.Unwrap(target.Type, left);
        var increment = mapping.Operation is SemanticProjectionOperation.Increment or SemanticProjectionOperation.Decrement;
        var one = primitive == SemanticPrimitiveType.DecimalNumber ? "java.math.BigDecimal.ONE" : "1L";
        var operandValue = source is null ? null : types.Unwrap(target.Type, source);
        var right = increment ? one : operandValue;
        if (right is null) return null;
        if (mapping.Factor is { } factor)
        {
            var operand = Value(factor, target.Type, @event);
            if (operand is null) return null;
            operand = types.Unwrap(target.Type, operand);
            right = primitive == SemanticPrimitiveType.DecimalNumber ? $"{right}.multiply({operand})" : $"({right} * {operand})";
        }
        var subtract = mapping.Operation is SemanticProjectionOperation.Subtract or SemanticProjectionOperation.Decrement;
        var result = primitive == SemanticPrimitiveType.DecimalNumber ? $"{left}.{(subtract ? "subtract" : "add")}({right})" : $"({left} {(subtract ? "-" : "+")} {right})";
        return types.Wrap(target.Type, result);
    }

    string? EventProperty(SemanticProjectionEventProperty property, SemanticTypeReference target, SemanticEventContract @event)
    {
        var path = Path(property.Path, @event.Properties, "event");
        if (path is null) return null;
        var source = path.Value.Type;
        if (source.Kind == target.Kind && source.Target == target.Target && source.Primitive == target.Primitive && source.IsCollection == target.IsCollection)
            return path.Value.Code;
        var value = types.Unwrap(source, path.Value.Code);
        var wrapped = types.Wrap(target, value);
        return source.IsOptional ? $"({path.Value.Code} == null ? null : {wrapped})" : wrapped;
    }

    string? EventKey(SemanticProjectionEventProperty property, SemanticEventContract @event)
    {
        var path = Path(property.Path, @event.Properties, "event");
        if (path is null) return null;
        return $"String.valueOf({types.Unwrap(path.Value.Type, path.Value.Code)})";
    }

    (string Code, SemanticTypeReference Type)? Path(IReadOnlyList<SemanticId> path, IReadOnlyList<SemanticProperty> properties, string root)
    {
        SemanticTypeReference? type = null;
        foreach (var id in path)
        {
            var property = properties.FirstOrDefault(candidate => candidate.Id == id);
            if (property is null) return null;
            root += "." + JavaNames.Member(property.Name) + "()";
            type = property.Type;
            properties = application.Types.FirstOrDefault(type => type.Id == property.Type.Target)?.Properties ?? [];
        }
        return type is null ? null : (root, type);
    }
}
