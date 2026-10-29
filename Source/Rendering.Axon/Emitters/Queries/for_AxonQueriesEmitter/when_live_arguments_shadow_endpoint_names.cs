// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_live_arguments_shadow_endpoint_names : given.a_query_collision
{
    [Theory]
    [InlineData("query", "var _query = new FindTotal(query);")]
    [InlineData("data", "var _data = results(query, metadata)")]
    [InlineData("values", "(first, _values) -> first.hasValue()")]
    [InlineData("shared", ".publish(_shared -> Flux.merge(1, _shared,")]
    [InlineData("gateway", "this.gateway.query(new FindTotal(gateway)")]
    [InlineData("heartbeat", "Flux.interval(this.heartbeat)")]
    [InlineData("timeout", ".take(this.timeout)")]
    [InlineData("shutdown", ".takeUntilOther(this.shutdown.asMono())")]
    [InlineData("result", ".map(_result ->")]
    [InlineData("payload", "Object _payload = result.rows()")]
    [InlineData("first", "(_first, values) -> _first.hasValue()")]
    [InlineData("tick", ".map(_tick ->")]
    public void should_preserve_the_wire_name_without_shadowing_java(string argument, string fragment)
    {
        _query = _query with { Delivery = SemanticQueryDelivery.Live, Argument = _query.Argument! with { Name = argument } };
        Render();
        var endpoint = Code("FindTotalEndpoint");
        endpoint.ShouldContain(fragment);
        endpoint.ShouldContain($"@RequestParam(name = \"{argument}\", required = true) String {argument}");
    }
}
