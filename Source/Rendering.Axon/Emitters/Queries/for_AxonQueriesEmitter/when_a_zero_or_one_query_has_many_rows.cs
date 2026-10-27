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

    [Fact] void should_allow_at_most_one_row_through_the_runtime() => Code("FindTotalHandler").ShouldContain("QueryResults.atMostOne(rows);");
    [Fact] void should_not_throw_an_unmapped_illegal_state() => Code("FindTotalHandler").ShouldNotContain("IllegalStateException");
}
