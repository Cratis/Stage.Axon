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
        var types = new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name));
        new QueryEndpoint(securedContext, types, located, query, types.Package(located) + "." + model.Name, new(query, model, types)).Emit();
        _securedEndpoint = Text(securedContext.Artifacts.Single());
        _endpoint = Source("LiveTotalsEndpoint.java");
        _subscription = Source("LiveTotalsSubscription.java");
        _projector = Source("TotalProjectionProjector.java");
    }

    [Fact] void should_authorize_both_mappings() => _securedEndpoint.Split("@PreAuthorize(\"denyAll()\")", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_map_the_stream_to_the_same_route() => _endpoint.ShouldContain("@GetMapping(value = \"/api/views/counters/live-totals\", produces = MediaType.TEXT_EVENT_STREAM_VALUE)");
    [Fact] void should_offer_a_plain_json_get() => _endpoint.ShouldContain("@GetMapping(\"/api/views/counters/live-totals\")");
    [Fact] void should_return_a_typed_list_snapshot() => _endpoint.ShouldContain("CompletableFuture<ResponseEntity<List<Total>>>");
    [Fact] void should_dispatch_the_snapshot_over_the_query_gateway() => _endpoint.ShouldContain("gateway.query(new LiveTotals(), LiveTotals.Result.class)");
    [Fact] void should_return_the_raw_snapshot_rows() => _endpoint.ShouldContain("ResponseEntity.ok(result.rows())");
    [Fact] void should_probe_a_plain_get_without_an_accept_header() => Source("LiveTotalsSseTest.java").ShouldContain("HttpRequest.newBuilder(request.uri()).GET().build()");
    [Fact] void should_return_an_sse_stream() => _endpoint.ShouldContain("streams.open(LiveTotalsSubscription.values(gateway, query))");
    [Fact] void should_use_an_axon_subscription_query() => _subscription.ShouldContain("gateway.subscriptionQuery(query, LiveTotals.Result.class)");
    [Fact] void should_delegate_to_latest_snapshot_delivery() => _subscription.ShouldContain("LiveSnapshots.of(() -> observe.apply(gateway.subscriptionQuery(query, LiveTotals.Result.class)), LiveTotals.Result::invalidated, () -> gateway.query(query, LiveTotals.Result.class))");
    [Fact] void should_not_carry_its_own_bridge() => _subscription.ShouldNotContain("class Bridge");
    [Fact] void should_delegate_streams_to_the_runtime_group() => Source("LiveTotalsStreams.java").ShouldContain("public LiveTotalsStreams(LiveStreams live)");
    [Fact] void should_open_sessions_through_the_group() => Source("LiveTotalsStreams.java").ShouldContain("group.open(publisher)");
    [Fact] void should_keep_the_active_count() => Source("LiveTotalsStreams.java").ShouldContain("int activeCount() { return group.activeCount(); }");
    [Fact] void should_leave_shutdown_to_the_runtime() => Source("LiveTotalsStreams.java").ShouldNotContain("ApplicationListener");
    [Fact] void should_invalidate_after_projecting_an_event() => _projector.ShouldContain("LiveTotals.Result::invalidation");
    [Fact] void should_resolve_updates_from_the_handler_context() => _projector.ShouldContain("event, QueryUpdateEmitter updates)");
    [Fact] void should_not_require_a_global_update_emitter() => _projector.ShouldNotContain("private final QueryUpdateEmitter");
    [Fact] void should_emit_an_initial_and_update_native_test() => Source("LiveTotalsSubscriptionTest.java").ShouldContain("shouldDeliverInitialSnapshotAndUpdateAndCancelAxonPublisher");
    [Fact] void should_serialize_expected_sse_values_with_the_application_concept_mapper() => Source("LiveTotalsSseTest.java").ShouldContain("var mapper = application.getBean(JsonMapper.class);");
    [Fact] void should_emit_an_http_sse_probe() => Source("LiveTotalsSseTest.java").ShouldContain("shouldDeliverInitialDataAndOneUpdateOverHttpSse");
    [Fact] void should_emit_a_five_second_shutdown_assertion() => Source("LiveTotalsSseTest.java").ShouldContain("elapsed.compareTo(Duration.ofSeconds(5)) < 0");
    [Fact] void should_not_degrade_live_queries_to_snapshots() => _context.Degradation.UnsupportedConstructs.Where(diagnostic => diagnostic.Artifact == QueryId("LiveTotals")).ShouldBeEmpty();

    string Source(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)));
}
