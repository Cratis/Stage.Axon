// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_live_query_is_named_live : a_query_collision
{
    string _streams;

    void Because()
    {
        _query = _query with { Name = "Live", Delivery = SemanticQueryDelivery.Live };
        Render();
        _streams = Code("LiveStreams");
    }

    [Fact] void should_qualify_the_runtime_streams() => _streams.ShouldContain("private final io.cratis.stage.axon.runtime.LiveStreams.Group group;");
    [Fact] void should_keep_the_generated_class_short() => _streams.ShouldContain("public final class LiveStreams {");
    [Fact] void should_not_import_the_runtime_streams() => _streams.ShouldNotContain("import io.cratis.stage.axon.runtime.LiveStreams;");
}
