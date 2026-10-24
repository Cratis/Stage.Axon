// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_emitting_live_queries : a_view_request
{
    AxonEmissionContext _context;
    string _endpoint;
    string _subscription;
    string _projector;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        _endpoint = Source("LiveTotalsEndpoint.java");
        _subscription = Source("LiveTotalsSubscription.java");
        _projector = Source("TotalProjectionProjector.java");
    }

    [Fact] void should_return_an_sse_stream() => _endpoint.ShouldContain("runtime.SseBridge.stream(LiveTotalsSubscription.values(gateway, query))");
    [Fact] void should_use_an_axon_subscription_query() => _subscription.ShouldContain("gateway.subscriptionQuery(query, LiveTotals.Result.class)");
    [Fact] void should_requery_the_original_filtered_query() => _subscription.ShouldContain("pending = gateway.query(query, LiveTotals.Result.class)");
    [Fact] void should_cancel_the_native_registration() => _subscription.ShouldContain("if (upstream != null) upstream.cancel()");
    [Fact] void should_cancel_pending_reads() => _subscription.ShouldContain("if (pending != null) pending.cancel(false)");
    [Fact] void should_invalidate_after_projecting_an_event() => _projector.ShouldContain("LiveTotals.Result::invalidation");
    [Fact] void should_resolve_updates_from_the_handler_context() => _projector.ShouldContain("event, org.axonframework.messaging.queryhandling.QueryUpdateEmitter updates)");
    [Fact] void should_not_require_a_global_update_emitter() => _projector.ShouldNotContain("private final org.axonframework.messaging.queryhandling.QueryUpdateEmitter");
    [Fact] void should_emit_an_initial_and_update_native_test() => Source("LiveTotalsSubscriptionTest.java").ShouldContain("shouldDeliverInitialSnapshotAndUpdateAndReleaseRegistration");
    [Fact] void should_emit_an_http_sse_probe() => Source("LiveTotalsSseTest.java").ShouldContain("shouldDeliverInitialDataAndOneUpdateOverHttpSse");
    [Fact] void should_not_degrade_live_queries_to_snapshots() => _context.Degradation.UnsupportedConstructs.Where(diagnostic => diagnostic.Artifact == QueryId("LiveTotals")).ShouldBeEmpty();

    string Source(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)));
}
