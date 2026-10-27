// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_live_helpers_use_a_read_model_from_another_package : given.a_query_collision
{
    void Because()
    {
        _context = new(_request, new(_request.Profile));
        LiveQuerySubscription.Emit(_context, _package, _query, "axonviews.other.Object");
        LiveQueryStreams.Emit(_context, _package, _query, "axonviews.other.Object");
    }

    [Fact] void should_not_import_the_indirect_subscription_model() => Code("FindTotalSubscription").ShouldNotContain("import axonviews.other.Object;");
    [Fact] void should_not_import_the_indirect_stream_model() => Code("FindTotalStreams").ShouldNotContain("import axonviews.other.Object;");
    [Fact] void should_keep_the_subscription_payload_short() => Code("FindTotalSubscription").ShouldContain("Flow.Publisher<Object> values(");
    [Fact] void should_keep_the_stream_payload_short() => Code("FindTotalStreams").ShouldContain("Flow.Publisher<Object> publisher");
}
