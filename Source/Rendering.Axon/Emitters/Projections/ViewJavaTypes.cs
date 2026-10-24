// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ViewJavaTypes(SemanticApplication application)
{
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
        ? application.Concepts.Single(concept => concept.Id == type.Target).Primitive : type.Primitive;

    internal string Wrap(SemanticTypeReference type, string value) => !type.IsCollection && type.Kind == SemanticTypeReferenceKind.Concept
        ? $"new {Type(type)}({value})" : value;

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
        SemanticTextValue text => Parse(type, JavaCodeBuilder.StringLiteral(text.Value)),
        SemanticNumberValue number => Wrap(type, PrimitiveOf(type) == SemanticPrimitiveType.DecimalNumber
            ? $"new java.math.BigDecimal({JavaCodeBuilder.StringLiteral(number.Value.ToString(CultureInfo.InvariantCulture))})"
            : number.Value.ToString(CultureInfo.InvariantCulture) + "L"),
        SemanticBooleanValue boolean => Wrap(type, boolean.Value ? "true" : "false"),
        _ => "null"
    };

    internal string Default(SemanticTypeReference type) => type.IsCollection ? "java.util.List.of()" : PrimitiveOf(type) switch
    {
        SemanticPrimitiveType.WholeNumber => Wrap(type, "0L"),
        SemanticPrimitiveType.DecimalNumber => Wrap(type, "java.math.BigDecimal.ZERO"),
        SemanticPrimitiveType.Boolean => Wrap(type, "false"),
        _ => "null"
    };

    internal string EventType(SemanticId id)
    {
        var owner = ScopeSelection.Locate(application).First(slice => slice.Slice.Events.Any(@event => @event.Id == id));
        return BasePackage + "." + JavaNames.PackageSegment(owner.Module.Name) + ".events." + JavaNames.Type(owner.Slice.Events.First(@event => @event.Id == id).Name);
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
}
