// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_the_read_model_is_named_list : given.a_query_collision
{
    void Because()
    {
        _model = _model with { Name = "List" };
        Render();
    }

    [Fact] void should_keep_the_same_package_model_short() => Code("FindTotal").ShouldContain("public record Result(java.util.List<List> rows)");
    [Fact] void should_not_import_the_conflicting_record_collection() => Code("FindTotal").ShouldNotContain("import java.util.List;");
    [Fact] void should_keep_the_handler_model_short() => Code("FindTotalHandler").ShouldContain("private final ListRepository repository;");
    [Fact] void should_qualify_the_performed_handler_collection() => Code("RankedTotalsHandler").ShouldContain("java.util.List.copyOf(performer.perform(query, repository))");
    [Fact] void should_qualify_the_endpoint_collection() => Code("LiveTotalsEndpoint").ShouldContain("CompletableFuture<ResponseEntity<java.util.List<List>>>");
    [Fact] void should_not_import_the_conflicting_endpoint_collection() => Code("LiveTotalsEndpoint").ShouldNotContain("import java.util.List;");
    [Fact] void should_qualify_the_performer_collection() => Code("RankedTotalsPerformer").ShouldContain("default java.util.List<List> perform(RankedTotals query, ListRepository repository)");
    [Fact] void should_not_import_the_conflicting_performer_collection() => Code("RankedTotalsPerformer").ShouldNotContain("import java.util.List;");
}
