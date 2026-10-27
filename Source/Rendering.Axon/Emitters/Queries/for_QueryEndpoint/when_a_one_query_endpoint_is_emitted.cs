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

    [Fact] void should_return_the_single_row() => Code("FindTotalEndpoint").ShouldContain("thenApply(result -> ResponseEntity.ok(result.rows().getFirst()))");
    [Fact] void should_not_map_a_missing_row_to_an_empty_not_found() => Code("FindTotalEndpoint").ShouldNotContain("notFound()");
}
