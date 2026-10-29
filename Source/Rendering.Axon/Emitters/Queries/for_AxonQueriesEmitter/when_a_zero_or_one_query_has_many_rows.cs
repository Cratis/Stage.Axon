// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_zero_or_one_query_has_many_rows : given.a_query_collision
{
    void Because()
    {
        _query = _query with { Cardinality = SemanticQueryCardinality.ZeroOrOne };
        Render();
    }

    [Fact] void should_return_the_rows_from_the_handler() => Code("FindTotalHandler").ShouldContain("return new FindTotal.Result(rows);");
    [Fact] void should_not_reference_the_runtime_query_helper() => Code("FindTotalHandler").ShouldNotContain("QueryResults");
    [Fact] void should_report_excess_rows_in_the_endpoint() => Code("FindTotalEndpoint").ShouldContain("if (result.rows().size() > 1) return ResponseEntity.of(cardinalityProblem(HttpStatus.INTERNAL_SERVER_ERROR, \"query-cardinality\", \"at most one\", result.rows().size())).build();");
    [Fact] void should_not_throw_an_unmapped_illegal_state() => Code("FindTotalHandler").ShouldNotContain("IllegalStateException");
}
