// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_the_read_model_is_named_read_model_store : given.a_query_collision
{
    string _subscription;
    string _sse;

    void Because()
    {
        _model = _model with { Name = "ReadModelStore" };
        Render();
        _subscription = Code("LiveTotalsSubscriptionTest");
        _sse = Code("LiveTotalsSseTest");
    }

    [Fact] void should_keep_the_application_model_short() => Code("FindTotal").ShouldContain("public record Result(List<ReadModelStore> rows)");
    [Fact] void should_type_the_handler_with_the_repository_of_the_model() => Code("FindTotalHandler").ShouldContain("private final ReadModelStoreRepository repository;");
    [Fact] void should_read_the_repository_in_the_subscription_spec() => _subscription.ShouldContain("ReadModelStoreRepository");
    [Fact] void should_read_the_repository_in_the_sse_spec() => _sse.ShouldContain("application.getBean(ReadModelStoreRepository.class)");
}
