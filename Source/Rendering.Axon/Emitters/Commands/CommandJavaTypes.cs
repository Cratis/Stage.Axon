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

    internal void Claim(SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (type.Kind is SemanticTypeReferenceKind.Concept or SemanticTypeReferenceKind.CompositeType)
        {
            var name = Type(type with { IsCollection = false });
            if (name.StartsWith(BasePackage + ".", StringComparison.Ordinal)) code.Import(name);
        }
    }

    internal string Type(SemanticTypeReference type, JavaCodeBuilder code)
    {
        var scalar = Type(type with { IsCollection = false });
        var name = Reference(scalar, code);
        return type.IsCollection ? $"{code.Reference("java.util.List")}<{name}>" : name;
    }

    internal string Reference(string type, JavaCodeBuilder code)
    {
        if (!type.StartsWith(BasePackage + ".", StringComparison.Ordinal))
        {
            return code.Reference(type.Contains('.', StringComparison.Ordinal) ? type : "java.lang." + type);
        }
        code.Import(type);
        return type[(type.LastIndexOf('.') + 1)..];
    }

    internal bool Cascades(SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.CompositeType ||
        (type.Kind == SemanticTypeReferenceKind.Concept && Concept(type.Target).Values.IsDefaultOrEmpty);

    internal SemanticPrimitiveType PrimitiveOf(SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.Concept ? Concept(type.Target).Primitive : type.Primitive;

    internal string Unwrap(string expression, SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.Concept ? expression + ".value()" : expression;

    internal string? Literal(SemanticValue value, SemanticTypeReference type)
    {
        if (value is SemanticNullValue) return "null";
        if (type.IsCollection && value is SemanticArrayValue array) return Array(array, type);
        if (type.Kind == SemanticTypeReferenceKind.CompositeType && value is SemanticCompositeValue composite) return CompositeLiteral(composite, type);
        return ScalarLiteral(value, type);
    }

    internal string? Literal(SemanticValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (Literal(value, type) is null) return null;
        ClaimLiteral(value, type, code);
        return RenderLiteral(value, type, code);
    }

    internal void ClaimLiteral(SemanticValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (value is SemanticNullValue || Literal(value, type) is null) return;
        if (type.IsCollection && value is SemanticArrayValue array)
        {
            foreach (var element in array.Values) ClaimLiteral(element, type with { IsCollection = false }, code);
            return;
        }
        if (type.Kind == SemanticTypeReferenceKind.Concept) Claim(type, code);
        ClaimCompositeValue(value, type, code);
    }

    internal string? Wrap(string value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        var concept = Concept(type.Target);
        if (concept.Numbers is not null) return null;
        var name = Type(type, code);
        return concept.Values.IsDefaultOrEmpty ? $"new {name}({value})" : $"{name}.fromValue({value})";
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

    static string Text(string value, SemanticPrimitiveType primitive, JavaCodeBuilder code)
    {
        var literal = JavaCodeBuilder.StringLiteral(value);
        if (primitive == SemanticPrimitiveType.Text) return literal;
        var method = primitive == SemanticPrimitiveType.Uuid ? "fromString" : "parse";
        var type = primitive switch
        {
            SemanticPrimitiveType.Uuid => "java.util.UUID",
            SemanticPrimitiveType.Date => "java.time.LocalDate",
            _ => "java.time.OffsetDateTime"
        };
        return $"{code.Reference(type)}.{method}({literal})";
    }

    static string? Scalar(SemanticValue value, SemanticPrimitiveType primitive, JavaCodeBuilder code) => value switch
    {
        SemanticTextValue text => Text(text.Value, primitive, code),
        SemanticNumberValue number when primitive == SemanticPrimitiveType.DecimalNumber => $"new {code.Reference("java.math.BigDecimal")}({JavaCodeBuilder.StringLiteral(number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))})",
        _ => Scalar(value, primitive)
    };

    string? RenderLiteral(SemanticValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (value is SemanticNullValue) return "null";
        if (type.IsCollection && value is SemanticArrayValue array) return Array(array, type, code);
        return value is SemanticCompositeValue composite && type.Kind == SemanticTypeReferenceKind.CompositeType
            ? CompositeLiteral(composite, type, code) : ScalarLiteral(value, type, code);
    }

    string Array(SemanticArrayValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        var elements = value.Values.Select(element => RenderLiteral(element, type with { IsCollection = false }, code)).ToArray();
        return $"{code.Reference("java.util.Arrays")}.asList({string.Join(", ", elements)})";
    }

    void ClaimCompositeValue(SemanticValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (value is SemanticCompositeValue composite && type.Kind == SemanticTypeReferenceKind.CompositeType) ClaimComposite(composite, type, code);
    }

    void ClaimComposite(SemanticCompositeValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        Claim(type, code);
        foreach (var property in Composite(type.Target).Properties)
        {
            if (value.Properties.FirstOrDefault(field => field.TargetProperty == property.Id) is { } field) ClaimLiteral(field.Value, property.Type, code);
        }
    }

    string CompositeLiteral(SemanticCompositeValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        var arguments = Composite(type.Target).Properties.Select(property => value.Properties.FirstOrDefault(field => field.TargetProperty == property.Id) is { } field
            ? RenderLiteral(field.Value, property.Type, code) : "null");
        return $"new {Type(type, code)}({string.Join(", ", arguments)})";
    }

    string? ScalarLiteral(SemanticValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (type.Kind == SemanticTypeReferenceKind.Concept && !Concept(type.Target).Values.IsDefaultOrEmpty)
        {
            var member = EnumLiteral(value, type)!;
            return Type(type, code) + member[member.LastIndexOf('.')..];
        }
        var literal = Scalar(value, PrimitiveOf(type), code);
        return literal is not null && type.Kind == SemanticTypeReferenceKind.Concept ? Wrap(literal, type, code) : literal;
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
