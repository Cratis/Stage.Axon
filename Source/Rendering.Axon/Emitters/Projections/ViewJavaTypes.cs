// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ViewJavaTypes(SemanticApplication application)
{
    static readonly Dictionary<SemanticPrimitiveType, (string Type, string Method)> _parsers = new()
    {
        [SemanticPrimitiveType.Uuid] = ("java.util.UUID", "fromString"),
        [SemanticPrimitiveType.WholeNumber] = ("java.lang.Long", "valueOf"),
        [SemanticPrimitiveType.Boolean] = ("java.lang.Boolean", "valueOf"),
        [SemanticPrimitiveType.Date] = ("java.time.LocalDate", "parse"),
        [SemanticPrimitiveType.DateTime] = ("java.time.OffsetDateTime", "parse")
    };

    readonly ViewConceptValues _concepts = new(application);
    internal string BasePackage { get; } = JavaNames.PackageSegment(application.Name);

    internal static void Add(AxonEmissionContext context, string package, string name, string source, params SemanticId[] ids) =>
        context.Artifacts.Add(Contracts.Rendering.PlannedArtifact.CreateText($"src/main/java/{package.Replace('.', '/')}/{name}.java", source, [.. ids]));

    internal string Package(LocatedSemanticSlice located) => BasePackage + "." + string.Join('.', located.Path.Select(JavaNames.PackageSegment));

    internal string Type(SemanticTypeReference type)
    {
        var scalar = type.Kind switch
        {
            SemanticTypeReferenceKind.Concept => BasePackage + ".concepts." + JavaNames.Type(application.Concepts.Single(concept => concept.Id == type.Target).Name),
            SemanticTypeReferenceKind.CompositeType => BasePackage + ".types." + JavaNames.Type(application.Types.Single(composite => composite.Id == type.Target).Name),
            _ => Primitive(type.Primitive)
        };
        return type.IsCollection ? $"java.util.List<{scalar}>" : scalar;
    }

    internal SemanticPrimitiveType PrimitiveOf(SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.Concept
        ? _concepts.Primitive(type) : type.Primitive;

    internal string Wrap(SemanticTypeReference type, string value) => !type.IsCollection && type.Kind == SemanticTypeReferenceKind.Concept
        ? _concepts.Wrap(type, Type(type), value) : value;

    internal string Unwrap(SemanticTypeReference type, string value) => !type.IsCollection && type.Kind == SemanticTypeReferenceKind.Concept ? $"{value}.value()" : value;

    internal string Parse(SemanticTypeReference type, string text) => Wrap(type, PrimitiveOf(type) switch
    {
        SemanticPrimitiveType.Uuid => $"java.util.UUID.fromString({text})",
        SemanticPrimitiveType.WholeNumber => $"Long.valueOf({text})",
        SemanticPrimitiveType.DecimalNumber => $"new java.math.BigDecimal({text})",
        SemanticPrimitiveType.Boolean => $"Boolean.valueOf({text})",
        SemanticPrimitiveType.Date => $"java.time.LocalDate.parse({text})",
        SemanticPrimitiveType.DateTime => $"java.time.OffsetDateTime.parse({text})",
        _ => text
    });

    internal string Literal(SemanticValue value, SemanticTypeReference type) => value switch
    {
        SemanticNullValue => "null",
        SemanticArrayValue array => $"java.util.List.of({string.Join(", ", array.Values.Select(element => Literal(element, type with { IsCollection = false, IsOptional = false })))})",
        SemanticCompositeValue composite => CompositeLiteral(composite, type),
        SemanticTextValue text => Parse(type, JavaCodeBuilder.StringLiteral(text.Value)),
        SemanticNumberValue number => Wrap(type, PrimitiveOf(type) == SemanticPrimitiveType.DecimalNumber
            ? $"new java.math.BigDecimal({JavaCodeBuilder.StringLiteral(number.Value.ToString(CultureInfo.InvariantCulture))})"
            : number.Value.ToString(CultureInfo.InvariantCulture) + "L"),
        SemanticBooleanValue boolean => Wrap(type, boolean.Value ? "true" : "false"),
        _ => "null"
    };

    internal string Default(SemanticTypeReference type) => type.IsCollection ? "java.util.List.of()" : ScalarDefault(type);

    internal string EventType(SemanticId id)
    {
        var owner = ScopeSelection.Locate(application).First(slice => slice.Slice.Events.Any(@event => @event.Id == id));
        return BasePackage + "." + JavaNames.PackageSegment(owner.Module.Name) + ".events." + JavaNames.Type(owner.Slice.Events.First(@event => @event.Id == id).Name);
    }

    internal void Claim(SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (type.Kind is SemanticTypeReferenceKind.Concept or SemanticTypeReferenceKind.CompositeType)
        {
            code.Reference(Type(type with { IsCollection = false }));
        }
    }

    internal string Type(SemanticTypeReference type, JavaCodeBuilder code)
    {
        var scalar = type.Kind is SemanticTypeReferenceKind.Concept or SemanticTypeReferenceKind.CompositeType
            ? Import(Type(type with { IsCollection = false }), code)
            : Primitive(type.Primitive, code);
        return type.IsCollection ? $"{code.Reference("java.util.List")}<{scalar}>" : scalar;
    }

    internal string EventType(SemanticId id, JavaCodeBuilder code) => Import(EventType(id), code);

    internal string Wrap(SemanticTypeReference type, string value, JavaCodeBuilder code) => !type.IsCollection && type.Kind == SemanticTypeReferenceKind.Concept
        ? _concepts.Wrap(type, Type(type, code), value) : value;

    internal string Parse(SemanticTypeReference type, string text, JavaCodeBuilder code)
    {
        if (!type.IsCollection && type.Kind == SemanticTypeReferenceKind.Concept) Type(type, code);
        return Wrap(type, ParsePrimitive(PrimitiveOf(type), text, code), code);
    }

    internal string Literal(SemanticValue value, SemanticTypeReference type, JavaCodeBuilder code) => value switch
    {
        SemanticArrayValue array => ArrayLiteral(array, type, code),
        SemanticCompositeValue composite => CompositeLiteral(composite, type, code),
        SemanticTextValue text => Parse(type, JavaCodeBuilder.StringLiteral(text.Value), code),
        SemanticNumberValue number => NumberLiteral(number, type, code),
        SemanticBooleanValue boolean => BooleanLiteral(boolean, type, code),
        _ => "null"
    };

    internal string Default(SemanticTypeReference type, JavaCodeBuilder code) => type.IsCollection
        ? $"{code.Reference("java.util.List")}.of()" : ScalarDefault(type, code);

    static string ParsePrimitive(SemanticPrimitiveType primitive, string text, JavaCodeBuilder code)
    {
        if (primitive == SemanticPrimitiveType.DecimalNumber) return $"new {code.Reference("java.math.BigDecimal")}({text})";
        return _parsers.TryGetValue(primitive, out var parser) ? $"{code.Reference(parser.Type)}.{parser.Method}({text})" : text;
    }

    static string Import(string type, JavaCodeBuilder code)
    {
        return code.Reference(type);
    }

    static string Primitive(SemanticPrimitiveType primitive, JavaCodeBuilder code)
    {
        var name = Primitive(primitive);
        return code.Reference(name.Contains('.', StringComparison.Ordinal) ? name : "java.lang." + name);
    }

    static string Primitive(SemanticPrimitiveType primitive) => primitive switch
    {
        SemanticPrimitiveType.Uuid => "java.util.UUID",
        SemanticPrimitiveType.WholeNumber => "Long",
        SemanticPrimitiveType.DecimalNumber => "java.math.BigDecimal",
        SemanticPrimitiveType.Boolean => "Boolean",
        SemanticPrimitiveType.Date => "java.time.LocalDate",
        SemanticPrimitiveType.DateTime => "java.time.OffsetDateTime",
        _ => "String"
    };

    string ScalarDefault(SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (_concepts.IsEnumeration(type)) return "null";
        return PrimitiveOf(type) switch
        {
            SemanticPrimitiveType.WholeNumber => Wrap(type, "0L", code),
            SemanticPrimitiveType.DecimalNumber => DecimalDefault(type, code),
            SemanticPrimitiveType.Boolean => Wrap(type, "false", code),
            _ => "null"
        };
    }

    string DecimalDefault(SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (type.Kind == SemanticTypeReferenceKind.Concept) Type(type, code);
        return Wrap(type, $"{code.Reference("java.math.BigDecimal")}.ZERO", code);
    }

    string ArrayLiteral(SemanticArrayValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        var elements = value.Values.Select(element => Literal(element, type with { IsCollection = false, IsOptional = false }, code)).ToArray();
        return $"{code.Reference("java.util.List")}.of({string.Join(", ", elements)})";
    }

    string NumberLiteral(SemanticNumberValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        if (!type.IsCollection && type.Kind == SemanticTypeReferenceKind.Concept) Type(type, code);
        var literal = PrimitiveOf(type) == SemanticPrimitiveType.DecimalNumber
            ? $"new {code.Reference("java.math.BigDecimal")}({JavaCodeBuilder.StringLiteral(value.Value.ToString(CultureInfo.InvariantCulture))})"
            : value.Value.ToString(CultureInfo.InvariantCulture) + "L";
        return Wrap(type, literal, code);
    }

    string BooleanLiteral(SemanticBooleanValue value, SemanticTypeReference type, JavaCodeBuilder code) => Wrap(type, value.Value ? "true" : "false", code);

    string CompositeLiteral(SemanticCompositeValue value, SemanticTypeReference type, JavaCodeBuilder code)
    {
        var name = Type(type, code);
        var composite = application.Types.First(candidate => candidate.Id == type.Target);
        var arguments = composite.Properties.Select(property => value.Properties.FirstOrDefault(entry => entry.TargetProperty == property.Id) is { } entry ? Literal(entry.Value, property.Type, code) : Default(property.Type, code));
        return $"new {name}({string.Join(", ", arguments)})";
    }

    string ScalarDefault(SemanticTypeReference type) => _concepts.IsEnumeration(type) ? "null" : PrimitiveOf(type) switch
    {
        SemanticPrimitiveType.WholeNumber => Wrap(type, "0L"),
        SemanticPrimitiveType.DecimalNumber => Wrap(type, "java.math.BigDecimal.ZERO"),
        SemanticPrimitiveType.Boolean => Wrap(type, "false"),
        _ => "null"
    };

    string CompositeLiteral(SemanticCompositeValue value, SemanticTypeReference type)
    {
        var composite = application.Types.First(candidate => candidate.Id == type.Target);
        var arguments = composite.Properties.Select(property => value.Properties.FirstOrDefault(entry => entry.TargetProperty == property.Id) is { } entry ? Literal(entry.Value, property.Type) : Default(property.Type));
        return $"new {Type(type)}({string.Join(", ", arguments)})";
    }
}
