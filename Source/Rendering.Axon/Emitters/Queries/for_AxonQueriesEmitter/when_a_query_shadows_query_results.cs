// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_query_shadows_query_results : given.a_query_collision
{
    void Because()
    {
        _query = _query with { Cardinality = SemanticQueryCardinality.One };
        _model = _model with { Name = "QueryResults" };
        Render();
    }

    [Fact] void should_qualify_the_runtime_helper() => Code("FindTotalHandler").ShouldContain("io.cratis.stage.axon.runtime.QueryResults.exactlyOne(rows);");
    [Fact] void should_not_import_the_runtime_helper() => Code("FindTotalHandler").ShouldNotContain("import io.cratis.stage.axon.runtime.QueryResults;");
}
