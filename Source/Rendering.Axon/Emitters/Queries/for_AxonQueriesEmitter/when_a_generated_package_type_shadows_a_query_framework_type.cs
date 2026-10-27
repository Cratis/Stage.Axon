// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_a_generated_package_type_shadows_a_query_framework_type : given.a_query_collision
{
    [Theory]
    [InlineData("Object", "LiveTotalsStreams", "Flow.Publisher<java.lang.Object>")]
    [InlineData("Override", "LiveTotalsSubscription", "@java.lang.Override")]
    [InlineData("Component", "FindTotalHandler", "@org.springframework.stereotype.Component(")]
    [InlineData("Bean", "RankedTotalsPerformerConfiguration", "@org.springframework.context.annotation.Bean(")]
    void should_reserve_types_already_generated_by_other_emitters(string name, string file, string expected)
    {
        _context = new(_request, new(_request.Profile));
        _context.AddJava(_package, name, $"package {_package};\npublic class {name} {{}}\n");
        new AxonQueriesEmitter().Emit(_context);
        Code(file).ShouldContain(expected);
    }
}
