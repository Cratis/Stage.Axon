// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_live_query_is_named_live : a_query_collision
{
    string _endpoint;

    void Because()
    {
        _query = _query with { Name = "Live", Delivery = SemanticQueryDelivery.Live };
        Render();
        _endpoint = Code("LiveEndpoint");
    }

    [Fact] void should_keep_the_query_type_short() => _endpoint.ShouldContain("results(Live query, Metadata metadata)");
    [Fact] void should_use_the_native_publisher() => _endpoint.ShouldContain("Flux<Live.Result>");
    [Fact] void should_not_import_runtime_streams() => _endpoint.ShouldNotContain("io.cratis.stage.axon.runtime");
}
