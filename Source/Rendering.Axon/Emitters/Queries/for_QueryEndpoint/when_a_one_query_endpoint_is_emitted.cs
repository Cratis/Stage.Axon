// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_QueryEndpoint;

public class when_a_one_query_endpoint_is_emitted : a_query_collision
{
    void Because()
    {
        _query = _query with { Cardinality = SemanticQueryCardinality.One };
        Render();
    }

    [Fact] void should_return_the_single_row() => Code("FindTotalEndpoint").ShouldContain("return ResponseEntity.ok(result.rows().getFirst());");
    [Fact] void should_return_a_missing_result_problem() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().isEmpty()) return ResponseEntity.of(cardinalityProblem(HttpStatus.NOT_FOUND, \"query-result-missing\", \"exactly one\", 0)).build();");
    [Fact] void should_return_an_excess_rows_problem() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().size() > 1) return ResponseEntity.of(cardinalityProblem(HttpStatus.INTERNAL_SERVER_ERROR, \"query-cardinality\", \"exactly one\", result.rows().size())).build();");
    [Fact] void should_keep_the_default_spring_problem_title() => Code("FindTotalEndpoint").ShouldNotContain("problem.setTitle(");
    [Fact] void should_set_the_problem_type() => Code("FindTotalEndpoint").ShouldContain("problem.setType(URI.create(\"urn:cratis:stage-axon:problem:\" + type));");
    [Fact] void should_expose_only_the_expected_cardinality() => Code("FindTotalEndpoint").ShouldContain("problem.setProperty(\"cardinality\", cardinality);");
    [Fact] void should_expose_the_count() => Code("FindTotalEndpoint").ShouldContain("problem.setProperty(\"count\", count);");
    [Fact] void should_use_a_fixed_problem_detail_without_row_data() => Code("FindTotalEndpoint").ShouldContain("ProblemDetail.forStatusAndDetail(status, \"Expected \" + cardinality + \" query result\")");
    [Fact] void should_not_map_a_missing_row_to_an_empty_not_found() => Code("FindTotalEndpoint").ShouldNotContain("notFound()");
}
