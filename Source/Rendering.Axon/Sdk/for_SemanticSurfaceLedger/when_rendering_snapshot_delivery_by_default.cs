// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_rendering_snapshot_delivery_by_default : a_view_request
{
    AxonEmissionContext _context;
    SemanticKeyedQuery _query;
    string _endpoint;
    string _handler;

    void Because()
    {
        _query = ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.Queries).Single(query => query.Name == "FindTotal");
        _context = new(_request, new(_request.Profile));
        new AxonQueriesEmitter().Emit(_context);
        _endpoint = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/FindTotalEndpoint.java", StringComparison.Ordinal)));
        _handler = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/FindTotalHandler.java", StringComparison.Ordinal)));
    }

    [Fact]
    public void should_render_the_snapshot_model_as_a_gateway_query()
    {
        Assert.Equal(SemanticQueryDelivery.Snapshot, _query.Delivery);
        Assert.Contains("/api/views/counters/find-total", _endpoint, StringComparison.Ordinal);
        Assert.Contains("gateway.query(new FindTotal(name), FindTotal.Result.class, metadata, null)", _endpoint, StringComparison.Ordinal);
        Assert.Contains("ResponseEntity.notFound().build()", _endpoint, StringComparison.Ordinal);
        Assert.Contains("Objects.equals(row.name(), query.name())", _handler, StringComparison.Ordinal);
        Assert.Contains("QueryResults.atMostOne(rows);", _handler, StringComparison.Ordinal);
        Assert.DoesNotContain(_context.Artifacts, artifact => artifact.RelativePath.EndsWith("/FindTotalStreams.java", StringComparison.Ordinal));
        Assert.DoesNotContain(_context.Artifacts, artifact => artifact.RelativePath.EndsWith("/FindTotalSubscription.java", StringComparison.Ordinal));
    }
}
