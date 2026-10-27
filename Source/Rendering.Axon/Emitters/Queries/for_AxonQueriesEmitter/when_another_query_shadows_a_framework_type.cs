// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_another_query_shadows_a_framework_type : given.a_query_collision
{
    [Theory]
    [InlineData("Object", "Flow.Publisher<java.lang.Object>")]
    [InlineData("Override", "@java.lang.Override")]
    [InlineData("Flow", "java.util.concurrent.Flow.Publisher<Object>")]
    void should_reserve_the_other_query_before_emitting_framework_references(string name, string expected)
    {
        _query = _query with { Name = name };
        Render();
        Code("LiveTotalsStreams").ShouldContain(expected);
    }
}
