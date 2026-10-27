// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_the_read_model_is_named_override : given.a_query_collision
{
    void Because()
    {
        _model = _model with { Name = "Override" };
        Render();
    }

    [Theory]
    [InlineData("LiveTotalsStreams")]
    [InlineData("LiveTotalsSubscription")]
    void should_qualify_the_framework_annotation(string file) => Code(file).ShouldContain("@java.lang.Override");

    [Theory]
    [InlineData("LiveTotalsStreams")]
    [InlineData("LiveTotalsSubscription")]
    void should_not_leave_any_shadowed_annotation(string file) => Code(file).ShouldNotContain("@Override");
}
