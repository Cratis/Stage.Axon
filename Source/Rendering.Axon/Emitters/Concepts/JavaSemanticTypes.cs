// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class JavaSemanticTypes
{
    internal static string Primitive(SemanticPrimitiveType primitive) => primitive switch
    {
        SemanticPrimitiveType.Uuid => "java.util.UUID",
        SemanticPrimitiveType.Text => "String",
        SemanticPrimitiveType.WholeNumber => "Long",
        SemanticPrimitiveType.DecimalNumber => "java.math.BigDecimal",
        SemanticPrimitiveType.Boolean => "Boolean",
        SemanticPrimitiveType.Date => "java.time.LocalDate",
        SemanticPrimitiveType.DateTime => "java.time.OffsetDateTime",
        _ => "Object"
    };

    internal static string TypeFor(AxonEmissionContext context, SemanticTypeReference type)
    {
        var name = type.Kind switch
        {
            SemanticTypeReferenceKind.Primitive => Primitive(type.Primitive),
            SemanticTypeReferenceKind.Concept => context.Request.Model.Application.Concepts.FirstOrDefault(concept => concept.Id == type.Target) is { } concept ? context.BasePackage + ".concepts." + JavaNames.Type(concept.Name) : "Object",
            SemanticTypeReferenceKind.CompositeType => context.Request.Model.Application.Types.FirstOrDefault(composite => composite.Id == type.Target) is { } composite ? context.BasePackage + ".types." + JavaNames.Type(composite.Name) : "Object",
            _ => "Object"
        };
        if (name == "Object") context.Degradation.Unsupported("STAGE-AXON-TYPE", type.Target, "The property type is not known to the Axon target.", "represent the modeled property type", type.Target + "/type");
        return type.IsCollection ? $"java.util.List<{name}>" : name;
    }
}
