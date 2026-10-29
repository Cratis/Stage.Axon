// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_one_query_has_no_or_many_rows : given.a_query_collision
{
    void Because()
    {
        _query = _query with { Cardinality = SemanticQueryCardinality.One };
        Render();
    }

    [Fact] void should_return_the_rows_from_the_handler() => Code("FindTotalHandler").ShouldContain("return new FindTotal.Result(rows);");
    [Fact] void should_not_reference_the_runtime_query_helper() => Code("FindTotalHandler").ShouldNotContain("QueryResults");
    [Fact] void should_report_a_missing_result_in_the_endpoint() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().isEmpty()) return ResponseEntity.of(cardinalityProblem(HttpStatus.NOT_FOUND, \"query-result-missing\", \"exactly one\", 0)).build();");
    [Fact] void should_report_excess_rows_in_the_endpoint() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().size() > 1) return ResponseEntity.of(cardinalityProblem(HttpStatus.INTERNAL_SERVER_ERROR, \"query-cardinality\", \"exactly one\", result.rows().size())).build();");
    [Fact] void should_not_throw_an_unmapped_illegal_state() => Code("FindTotalHandler").ShouldNotContain("IllegalStateException");
}
