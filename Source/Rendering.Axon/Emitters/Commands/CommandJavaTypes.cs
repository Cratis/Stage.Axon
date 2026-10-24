// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandJavaTypes(SemanticApplication application)
{
    static readonly IReadOnlyDictionary<SemanticPrimitiveType, string> _primitives = new Dictionary<SemanticPrimitiveType, string>
    {
        [SemanticPrimitiveType.Uuid] = "java.util.UUID",
        [SemanticPrimitiveType.Text] = "String",
        [SemanticPrimitiveType.WholeNumber] = "Long",
        [SemanticPrimitiveType.DecimalNumber] = "java.math.BigDecimal",
        [SemanticPrimitiveType.Boolean] = "Boolean",
        [SemanticPrimitiveType.Date] = "java.time.LocalDate",
        [SemanticPrimitiveType.DateTime] = "java.time.OffsetDateTime"
    };

    internal string BasePackage { get; } = JavaNames.PackageSegment(application.Name);

    internal string Type(SemanticTypeReference type)
    {
        var scalar = type.Kind switch
        {
            SemanticTypeReferenceKind.Primitive => _primitives.GetValueOrDefault(type.Primitive, "Object"),
            SemanticTypeReferenceKind.Concept => BasePackage + ".concepts." + JavaNames.Type(Concept(type.Target).Name),
            SemanticTypeReferenceKind.CompositeType => BasePackage + ".types." + JavaNames.Type(Composite(type.Target).Name),
            _ => "Object"
        };
        return type.IsCollection ? $"java.util.List<{scalar}>" : scalar;
    }

    internal SemanticPrimitiveType PrimitiveOf(SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.Concept ? Concept(type.Target).Primitive : type.Primitive;

    internal string Unwrap(string expression, SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.Concept ? expression + ".value()" : expression;

    internal string? Literal(SemanticValue value, SemanticTypeReference type)
    {
        if (value is SemanticNullValue) return "null";
        if (type.IsCollection && value is SemanticArrayValue array) return Array(array, type);
        if (type.Kind == SemanticTypeReferenceKind.CompositeType && value is SemanticCompositeValue composite) return CompositeLiteral(composite, type);
        return ScalarLiteral(value, type);
    }

    internal string? Wrap(string value, SemanticTypeReference type)
    {
        var concept = Concept(type.Target);
        if (concept.Numbers is not null) return null;
        return concept.Values.IsDefaultOrEmpty ? $"new {Type(type)}({value})" : $"{Type(type)}.fromValue({value})";
    }

    static string? Scalar(SemanticValue value, SemanticPrimitiveType primitive) => value switch
    {
        SemanticTextValue text => Text(text.Value, primitive),
        SemanticNumberValue number => Number(number.Value, primitive),
        SemanticBooleanValue boolean when primitive == SemanticPrimitiveType.Boolean => boolean.Value ? "true" : "false",
        _ => null
    };

    static string? Text(string value, SemanticPrimitiveType primitive)
    {
        var literal = JavaCodeBuilder.StringLiteral(value);
        return primitive switch
        {
            SemanticPrimitiveType.Text => literal,
            SemanticPrimitiveType.Uuid => $"java.util.UUID.fromString({literal})",
            SemanticPrimitiveType.Date => $"java.time.LocalDate.parse({literal})",
            SemanticPrimitiveType.DateTime => $"java.time.OffsetDateTime.parse({literal})",
            _ => null
        };
    }

    static string? Number(decimal value, SemanticPrimitiveType primitive)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (primitive == SemanticPrimitiveType.DecimalNumber) return $"new java.math.BigDecimal({JavaCodeBuilder.StringLiteral(text)})";
        if (primitive != SemanticPrimitiveType.WholeNumber) return null;
        return value == decimal.Truncate(value) && value >= long.MinValue && value <= long.MaxValue ? text + "L" : null;
    }

    string? Array(SemanticArrayValue array, SemanticTypeReference type)
    {
        var elements = array.Values.Select(element => Literal(element, type with { IsCollection = false })).ToArray();
        return elements.Any(element => element is null) ? null : $"java.util.Arrays.asList({string.Join(", ", elements)})";
    }

    string? CompositeLiteral(SemanticCompositeValue value, SemanticTypeReference type)
    {
        var arguments = Composite(type.Target).Properties.Select(property => CompositeProperty(value, property)).ToArray();
        return arguments.Any(argument => argument is null) ? null : $"new {Type(type)}({string.Join(", ", arguments)})";
    }

    string? ScalarLiteral(SemanticValue value, SemanticTypeReference type)
    {
        if (type.Kind == SemanticTypeReferenceKind.Concept && !Concept(type.Target).Values.IsDefaultOrEmpty) return EnumLiteral(value, type);
        var literal = Scalar(value, PrimitiveOf(type));
        return literal is not null && type.Kind == SemanticTypeReferenceKind.Concept ? Wrap(literal, type) : literal;
    }

    string? CompositeProperty(SemanticCompositeValue value, SemanticProperty property)
    {
        var field = value.Properties.FirstOrDefault(value => value.TargetProperty == property.Id);
        if (field is not null) return Literal(field.Value, property.Type);
        return property.Type.IsOptional ? "null" : null;
    }

    string? EnumLiteral(SemanticValue value, SemanticTypeReference type)
    {
        var concept = Concept(type.Target);
        var index = value switch
        {
            SemanticTextValue text => concept.Values.IndexOf(text.Value),
            SemanticNumberValue number when number.Value == decimal.Truncate(number.Value) && number.Value >= int.MinValue && number.Value <= int.MaxValue => concept.Numbers?.IndexOf((int)number.Value) ?? -1,
            _ => -1
        };
        return index < 0 ? null : Type(type) + "." + JavaNames.Member(concept.Values[index]).ToUpperInvariant();
    }

    SemanticConcept Concept(SemanticId id) => application.Concepts.Single(concept => concept.Id == id);

    SemanticCompositeType Composite(SemanticId id) => application.Types.Single(composite => composite.Id == id);
}
