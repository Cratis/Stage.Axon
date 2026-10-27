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

    [Fact] void should_keep_the_empty_not_found() => Code("FindTotalEndpoint").ShouldContain("result.rows().isEmpty() ? ResponseEntity.notFound().build() : ResponseEntity.ok(result.rows().getFirst())");
}
