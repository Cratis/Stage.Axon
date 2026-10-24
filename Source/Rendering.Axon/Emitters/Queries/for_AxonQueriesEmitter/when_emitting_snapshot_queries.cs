// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_emitting_snapshot_queries : a_view_request
{
    AxonEmissionContext _context;
    string _handler;
    string _endpoint;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonQueriesEmitter().Emit(_context);
        _handler = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/FindTotalHandler.java", StringComparison.Ordinal)));
        _endpoint = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/FindTotalEndpoint.java", StringComparison.Ordinal)));
    }

    [Fact] void should_register_an_axon_query_handler() => _handler.ShouldContain("@QueryHandler");
    [Fact] void should_match_the_key() => _handler.ShouldContain("java.util.Objects.equals(row.name(), query.name())");
    [Fact] void should_reject_non_unique_optional_results() => _handler.ShouldContain("if (rows.size() > 1)");
    [Fact] void should_dispatch_over_the_query_gateway() => _endpoint.ShouldContain("gateway.query(new FindTotal(name), FindTotal.Result.class)");
    [Fact] void should_return_not_found_for_an_empty_optional_result() => _endpoint.ShouldContain("ResponseEntity.notFound().build()");
    [Fact] void should_expose_the_query_kebab_route() => _endpoint.ShouldContain("/api/views/counters/find-total");
    [Fact] void should_have_no_degradations() => _context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
}
