// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ViewConceptValues(SemanticApplication application)
{
    internal bool IsEnumeration(SemanticTypeReference type) => type.Kind == SemanticTypeReferenceKind.Concept
        && !Concept(type).Values.IsEmpty;

    internal SemanticPrimitiveType Primitive(SemanticTypeReference type)
    {
        var concept = Concept(type);
        return Numbered(concept) ? SemanticPrimitiveType.WholeNumber : concept.Primitive;
    }

    internal string Wrap(SemanticTypeReference type, string name, string value)
    {
        var concept = Concept(type);
        if (concept.Values.IsEmpty) return $"new {name}({value})";
        var argument = Numbered(concept) ? $"Math.toIntExact({value})" : value;
        return $"{name}.fromValue({argument})";
    }

    static bool Numbered(SemanticConcept concept) => concept.Numbers is { } numbers
        && !concept.Values.IsEmpty && numbers.Length == concept.Values.Length;

    SemanticConcept Concept(SemanticTypeReference type) => application.Concepts.Single(concept => concept.Id == type.Target);
}
