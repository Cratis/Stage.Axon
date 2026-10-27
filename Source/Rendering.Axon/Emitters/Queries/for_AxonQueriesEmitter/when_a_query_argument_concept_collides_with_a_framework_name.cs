// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_query_argument_concept_collides_with_a_framework_name : given.a_query_collision
{
    void Because()
    {
        var concept = new SemanticConcept(SemanticId.Create(SemanticKind.Concept, "String"), "String", SemanticPrimitiveType.Text, [], []);
        var type = new SemanticTypeReference(SemanticTypeReferenceKind.Concept, SemanticPrimitiveType.Unknown, concept.Id, false, false);
        _query = _query with { Argument = _query.Argument with { Type = type } };
        _model = _model with { Properties = [.. _model.Properties.Select(property => property.Id == _query.KeyProperty ? property with { Type = type } : property)] };
        Render(concept);
    }

    [Fact] void should_import_the_argument_concept() => Code("FindTotal").ShouldContain("import axonviews.concepts.String;");
    [Fact] void should_keep_the_argument_concept_short() => Code("FindTotal").ShouldContain("public record FindTotal(String name)");
    [Fact] void should_import_the_endpoint_concept() => Code("FindTotalEndpoint").ShouldContain("import axonviews.concepts.String;");
    [Fact] void should_qualify_the_framework_parameter() => Code("FindTotalEndpoint").ShouldContain("@RequestParam(name = \"name\", required = true) java.lang.String name");
    [Fact] void should_construct_the_argument_concept() => Code("FindTotalEndpoint").ShouldContain("new FindTotal(new String(name))");
    [Fact] void should_not_import_the_conflicting_framework_type() => Code("FindTotalEndpoint").ShouldNotContain("import java.lang.String;");
    [Fact] void should_not_import_the_unused_handler_concept() => Code("FindTotalHandler").ShouldNotContain("import axonviews.concepts.String;");
}
