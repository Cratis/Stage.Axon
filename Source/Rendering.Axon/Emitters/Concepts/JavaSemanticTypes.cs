// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class JavaSemanticTypes
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

    internal static string Primitive(SemanticPrimitiveType primitive) => _primitives.GetValueOrDefault(primitive, "Object");

    internal static string TypeFor(AxonEmissionContext context, SemanticTypeReference type)
    {
        var name = ReferenceName(context, type);
        if (name == "Object") context.Degradation.Unsupported("STAGE-AXON-TYPE", type.Target, "The property type is not known to the Axon target.", "represent the modeled property type", type.Target + "/type");
        return type.IsCollection ? $"java.util.List<{name}>" : name;
    }

    static string ReferenceName(AxonEmissionContext context, SemanticTypeReference type) => type.Kind switch
    {
        SemanticTypeReferenceKind.Primitive => Primitive(type.Primitive),
        SemanticTypeReferenceKind.Concept => ConceptName(context, type.Target),
        SemanticTypeReferenceKind.CompositeType => CompositeName(context, type.Target),
        _ => "Object"
    };

    static string ConceptName(AxonEmissionContext context, SemanticId target)
    {
        var concept = context.Request.Model.Application.Concepts.FirstOrDefault(concept => concept.Id == target);
        return concept is null ? "Object" : context.BasePackage + ".concepts." + JavaNames.Type(concept.Name);
    }

    static string CompositeName(AxonEmissionContext context, SemanticId target)
    {
        var composite = context.Request.Model.Application.Types.FirstOrDefault(composite => composite.Id == target);
        return composite is null ? "Object" : context.BasePackage + ".types." + JavaNames.Type(composite.Name);
    }
}
