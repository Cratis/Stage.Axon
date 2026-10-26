// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_query_and_its_concept_share_a_name : given.a_query_collision
{
    void Because()
    {
        var concept = new SemanticConcept(SemanticId.Create(SemanticKind.Concept, "FindTotal"), "FindTotal", SemanticPrimitiveType.Text, [], []);
        var type = new SemanticTypeReference(SemanticTypeReferenceKind.Concept, SemanticPrimitiveType.Unknown, concept.Id, false, false);
        _query = _query with { Argument = _query.Argument with { Type = type } };
        _model = _model with { Properties = [.. _model.Properties.Select(property => property.Id == _query.KeyProperty ? property with { Type = type } : property)] };
        Render(concept);
    }

    [Fact] void should_qualify_the_argument_concept() => Code("FindTotal").ShouldContain("public record FindTotal(axonviews.concepts.FindTotal name)");
    [Fact] void should_not_import_the_shadowed_concept() => Code("FindTotal").ShouldNotContain("import axonviews.concepts.FindTotal;");
    [Fact] void should_qualify_concept_construction_in_the_endpoint() => Code("FindTotalEndpoint").ShouldContain("new FindTotal(new axonviews.concepts.FindTotal(name))");
    [Fact] void should_not_import_the_endpoints_own_package_model() => Code("FindTotalEndpoint").ShouldNotContain($"import {_package}.Total;");
}
