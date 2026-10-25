// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_emitting_live_queries : a_view_request
{
    AxonEmissionContext _context;
    string _endpoint;
    string _subscription;
    string _projector;
    string _securedEndpoint;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "LiveTotals"));
        var query = located.Slice.Queries.Single(query => query.Name == "LiveTotals") with { Authorization = new SemanticPolicyReference("Unavailable") };
        var model = located.Slice.ReadModels.Single(model => model.Id == query.ReadModel);
        var securedContext = new AxonEmissionContext(_request, new(_request.Profile));
        var types = new ViewJavaTypes(_request.Model.Application);
        new QueryEndpoint(securedContext, types, located, query, types.Package(located) + "." + model.Name, new(query, model, types)).Emit();
        _securedEndpoint = Text(securedContext.Artifacts.Single());
        _endpoint = Source("LiveTotalsEndpoint.java");
        _subscription = Source("LiveTotalsSubscription.java");
        _projector = Source("TotalProjectionProjector.java");
    }

    [Fact] void should_authorize_both_mappings() => _securedEndpoint.Split("@PreAuthorize(\"denyAll()\")", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_map_the_stream_to_the_same_route() => _endpoint.ShouldContain("@GetMapping(value = \"/api/views/counters/live-totals\", produces = org.springframework.http.MediaType.TEXT_EVENT_STREAM_VALUE)");
    [Fact] void should_offer_a_plain_json_get() => _endpoint.ShouldContain("@GetMapping(\"/api/views/counters/live-totals\")");
    [Fact] void should_return_a_typed_list_snapshot() => _endpoint.ShouldContain("CompletableFuture<ResponseEntity<java.util.List<axonviews.views.counters.totals.Total>>>");
    [Fact] void should_dispatch_the_snapshot_over_the_query_gateway() => _endpoint.ShouldContain("gateway.query(new LiveTotals(), LiveTotals.Result.class)");
    [Fact] void should_return_the_raw_snapshot_rows() => _endpoint.ShouldContain("ResponseEntity.ok(result.rows())");
    [Fact] void should_probe_a_plain_get_without_an_accept_header() => Source("LiveTotalsSseTest.java").ShouldContain("HttpRequest.newBuilder(request.uri()).GET().build()");
    [Fact] void should_return_an_sse_stream() => _endpoint.ShouldContain("streams.open(LiveTotalsSubscription.values(gateway, query))");
    [Fact] void should_use_an_axon_subscription_query() => _subscription.ShouldContain("gateway.subscriptionQuery(query, LiveTotals.Result.class)");
    [Fact] void should_requery_the_original_filtered_query() => _subscription.ShouldContain("pending = gateway.query(query, LiveTotals.Result.class)");
    [Fact] void should_cancel_the_native_registration() => _subscription.ShouldContain("if (upstream != null) upstream.cancel()");
    [Fact] void should_cancel_pending_reads() => _subscription.ShouldContain("if (pending != null) pending.cancel(false)");
    [Fact] void should_invalidate_after_projecting_an_event() => _projector.ShouldContain("LiveTotals.Result::invalidation");
    [Fact] void should_resolve_updates_from_the_handler_context() => _projector.ShouldContain("event, org.axonframework.messaging.queryhandling.QueryUpdateEmitter updates)");
    [Fact] void should_not_require_a_global_update_emitter() => _projector.ShouldNotContain("private final org.axonframework.messaging.queryhandling.QueryUpdateEmitter");
    [Fact] void should_emit_an_initial_and_update_native_test() => Source("LiveTotalsSubscriptionTest.java").ShouldContain("shouldDeliverInitialSnapshotAndUpdateAndCancelAxonPublisher");
    [Fact] void should_serialize_expected_sse_values_with_the_application_concept_mapper() => Source("LiveTotalsSseTest.java").ShouldContain("var mapper = application.getBean(tools.jackson.databind.json.JsonMapper.class);");
    [Fact] void should_emit_an_http_sse_probe() => Source("LiveTotalsSseTest.java").ShouldContain("shouldDeliverInitialDataAndOneUpdateOverHttpSse");
    [Fact] void should_complete_active_streams_before_web_server_shutdown() => Source("LiveTotalsStreams.java").ShouldContain("ApplicationListener<org.springframework.context.event.ContextClosedEvent>");
    [Fact] void should_close_every_session_on_context_close() => Source("LiveTotalsStreams.java").ShouldContain("sessions.forEach(Session::stop)");
    [Fact] void should_cancel_the_session_upstream_on_shutdown() => Source("LiveTotalsStreams.java").ShouldContain("if (active != null) active.cancel()");
    [Fact] void should_complete_the_session_emitter_on_shutdown() => Source("LiveTotalsStreams.java").ShouldContain("if (active != null) active.complete()");
    [Fact] void should_emit_a_five_second_shutdown_assertion() => Source("LiveTotalsSseTest.java").ShouldContain("elapsed.compareTo(java.time.Duration.ofSeconds(5)) < 0");
    [Fact] void should_not_degrade_live_queries_to_snapshots() => _context.Degradation.UnsupportedConstructs.Where(diagnostic => diagnostic.Artifact == QueryId("LiveTotals")).ShouldBeEmpty();

    string Source(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)));
}
