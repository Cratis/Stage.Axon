// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandJavaTypes(SemanticApplication application)
{
    static readonly IReadOnlyDictionary<SemanticPrimitiveType, string> _primitives = new Dictionary<SemanticPrimitiveType, string>
    {
        [SemanticPrimitiveType.Uuid] = "java.util.UUID", [SemanticPrimitiveType.Text] = "String",
        [SemanticPrimitiveType.WholeNumber] = "Long", [SemanticPrimitiveType.DecimalNumber] = "java.math.BigDecimal",
        [SemanticPrimitiveType.Boolean] = "Boolean", [SemanticPrimitiveType.Date] = "java.time.LocalDate",
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
        var literal = Scalar(value, PrimitiveOf(type));
        return literal is not null && type.Kind == SemanticTypeReferenceKind.Concept ? $"new {Type(type)}({literal})" : literal;
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

    SemanticConcept Concept(SemanticId id) => application.Concepts.Single(concept => concept.Id == id);

    SemanticCompositeType Composite(SemanticId id) => application.Types.Single(composite => composite.Id == id);
}
