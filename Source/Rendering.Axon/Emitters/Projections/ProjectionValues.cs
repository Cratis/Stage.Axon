// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionValues(AxonEmissionContext context, ViewJavaTypes types)
{
    readonly SemanticApplication _application = context.Request.Model.Application;

    internal string Source(SemanticEventContract @event) => $"String.valueOf(event.{AxonEventSources.PrimaryTag(context, @event).ComponentName}().value())";
    internal string? Value(SemanticProjectionValue? value, SemanticTypeReference target, SemanticEventContract @event) => value switch
    {
        SemanticProjectionEventProperty property => EventProperty(property, target, @event),
        SemanticProjectionEventSourceIdentity => types.Parse(target, Source(@event)),
        SemanticProjectionLiteral literal when literal.Value is SemanticTextValue or SemanticNumberValue or SemanticBooleanValue or SemanticNullValue or SemanticArrayValue or SemanticCompositeValue => types.Literal(literal.Value, target),
        SemanticProjectionEventContextValue context => Context(context.Path, target, @event),
        _ => null
    };

    internal string? Key(SemanticProjectionKey key, SemanticEventContract @event) => key switch
    {
        SemanticProjectionValueKey { Value: SemanticProjectionEventSourceIdentity } => Source(@event),
        SemanticProjectionValueKey { Value: SemanticProjectionLiteral { Value: SemanticTextValue literal } } => JavaCodeBuilder.StringLiteral(literal.Value),
        SemanticProjectionValueKey { Value: SemanticProjectionLiteral { Value: SemanticNumberValue literal } } => JavaCodeBuilder.StringLiteral(literal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        SemanticProjectionValueKey { Value: SemanticProjectionLiteral { Value: SemanticBooleanValue literal } } => JavaCodeBuilder.StringLiteral(literal.Value ? "true" : "false"),
        SemanticProjectionValueKey { Value: SemanticProjectionEventProperty property } => EventKey(property, @event),
        SemanticProjectionCompositeKey composite => Composite(composite, @event),
        SemanticProjectionPartsKey parts => Parts(parts, @event),
        _ => null
    };

    internal string? Mapping(SemanticProjectionMapping mapping, SemanticProperty target, SemanticEventContract @event, string? previous = null)
    {
        var old = previous ?? $"state.{JavaNames.Member(target.Name)}()";
        if (mapping.Operation == SemanticProjectionOperation.Clear) return target.Type.IsCollection ? "java.util.List.of()" : "null";
        var source = Value(mapping.Source, target.Type, @event);
        if (mapping.Operation == SemanticProjectionOperation.Set) return source;
        if (mapping.Operation is not (SemanticProjectionOperation.Add or SemanticProjectionOperation.Subtract or SemanticProjectionOperation.Increment or SemanticProjectionOperation.Decrement)) return null;
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

    internal string? CompositeValue(SemanticProjectionCompositeKey key, SemanticEventContract @event)
    {
        var composite = _application.Types.First(type => type.Id == key.Type);
        var arguments = composite.Properties.Select(property => key.Parts.FirstOrDefault(part => part.Property == property.Id) is { } part ? Value(part.Value, property.Type, @event) : null).ToArray();
        return arguments.Any(argument => argument is null) ? null : $"new {types.BasePackage}.types.{JavaNames.Type(composite.Name)}({string.Join(", ", arguments)})";
    }

    string? Context(string path, SemanticTypeReference target, SemanticEventContract @event)
    {
        var name = path.TrimStart('$').Replace("eventContext.", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (name.Equals("eventSourceId", StringComparison.OrdinalIgnoreCase)) return types.Parse(target, Source(@event));
        if (name.Equals("occurred", StringComparison.OrdinalIgnoreCase) || name.Equals("occurredAt", StringComparison.OrdinalIgnoreCase))
            return types.Wrap(target, "java.time.OffsetDateTime.ofInstant(message == null ? clock.instant() : message.timestamp(), java.time.ZoneOffset.UTC)");
        if (name.Equals("correlationId", StringComparison.OrdinalIgnoreCase) || name.Equals("causationId", StringComparison.OrdinalIgnoreCase))
        {
            var value = $"message.metadata().get({JavaCodeBuilder.StringLiteral(name)})";
            return $"(message == null || {value} == null ? null : {types.Parse(target, value)})";
        }
        return null;
    }

    string? Composite(SemanticProjectionCompositeKey key, SemanticEventContract @event) => CompositeValue(key, @event) is { } value ? $"String.valueOf({value})" : null;

    string? Parts(SemanticProjectionPartsKey key, SemanticEventContract @event)
    {
        var properties = _application.Types.SelectMany(type => type.Properties).Concat(Sdk.ScopeSelection.Locate(_application).SelectMany(slice => slice.Slice.ReadModels).SelectMany(model => model.Properties));
        var entries = key.Parts.Select(part =>
        {
            var property = properties.First(candidate => candidate.Id == part.Property);
            var value = Value(part.Value, property.Type, @event);
            return value is null ? null : $"java.util.Map.entry({JavaCodeBuilder.StringLiteral(part.Property.ToString())}, String.valueOf({types.Unwrap(property.Type, value)}))";
        }).ToArray();
        return entries.Any(entry => entry is null) ? null : $"java.util.Map.ofEntries({string.Join(", ", entries)})";
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
            properties = _application.Types.FirstOrDefault(type => type.Id == property.Type.Target)?.Properties ?? [];
        }
        return type is null ? null : (root, type);
    }
}
