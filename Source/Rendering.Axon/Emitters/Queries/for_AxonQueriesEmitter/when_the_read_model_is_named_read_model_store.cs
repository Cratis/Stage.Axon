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
    [Fact] void should_qualify_the_runtime_store() => Code("FindTotalHandler").ShouldContain("io.cratis.stage.axon.runtime.ReadModelStore<Object, ReadModelStore> store;");
    [Fact] void should_not_import_the_conflicting_runtime_store() => Code("FindTotalHandler").ShouldNotContain("import io.cratis.stage.axon.runtime.ReadModelStore;");
    [Fact] void should_keep_the_subscription_model_short() => _subscription.ShouldContain("private io.cratis.stage.axon.runtime.ReadModelStore<Object, ReadModelStore> store;");
    [Fact] void should_not_import_the_subscription_runtime_store() => _subscription.ShouldNotContain("import io.cratis.stage.axon.runtime.ReadModelStore;");
    [Fact] void should_qualify_the_sse_runtime_store() => _sse.ShouldContain(", io.cratis.stage.axon.runtime.ReadModelStore.class);");
    [Fact] void should_not_import_the_sse_runtime_store() => _sse.ShouldNotContain("import io.cratis.stage.axon.runtime.ReadModelStore;");
}
