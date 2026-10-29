// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_QueryEndpoint;

public class when_a_zero_or_one_query_endpoint_is_emitted : a_query_collision
{
    void Because()
    {
        _query = _query with { Cardinality = SemanticQueryCardinality.ZeroOrOne };
        Render();
    }

    [Fact] void should_keep_the_empty_not_found() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().isEmpty()) return ResponseEntity.notFound().build();");
    [Fact] void should_return_the_single_row() => Code("FindTotalEndpoint").ShouldContain("return ResponseEntity.ok(result.rows().getFirst());");
    [Fact] void should_reject_excess_rows() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().size() > 1) return ResponseEntity.of(cardinalityProblem(HttpStatus.INTERNAL_SERVER_ERROR, \"query-cardinality\", \"at most one\", result.rows().size())).build();");
    [Fact] void should_not_emit_a_missing_result_problem() => Code("FindTotalEndpoint").ShouldNotContain("\"query-result-missing\"");
}
