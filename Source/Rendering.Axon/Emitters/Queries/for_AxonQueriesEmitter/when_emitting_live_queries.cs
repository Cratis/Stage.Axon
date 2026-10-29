// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_emitting_live_queries : a_view_request
{
    AxonEmissionContext _context;
    string _endpoint;
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
        _projector = Source("TotalProjectionProjector.java");
        UnsupportedSummary.Emit(_context);
    }

    [Fact] void should_authorize_both_mappings() => _securedEndpoint.Split("@PreAuthorize(\"denyAll()\")", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_map_the_stream_to_the_same_route() => _endpoint.ShouldContain("@GetMapping(value = \"/api/views/counters/live-totals\", produces = MediaType.TEXT_EVENT_STREAM_VALUE)");
    [Fact] void should_offer_a_plain_json_get() => _endpoint.ShouldContain("@GetMapping(\"/api/views/counters/live-totals\")");
    [Fact] void should_return_a_typed_list_snapshot() => _endpoint.ShouldContain("CompletableFuture<ResponseEntity<List<Total>>>");
    [Fact] void should_dispatch_the_snapshot_over_the_query_gateway() => _endpoint.ShouldContain("gateway.query(new LiveTotals(), LiveTotals.Result.class, metadata, null)");
    [Fact] void should_return_the_raw_snapshot_rows() => _endpoint.ShouldContain("ResponseEntity.ok(result.rows())");
    [Fact] void should_probe_a_plain_get_without_an_accept_header() => Source("LiveTotalsSseTest.java").ShouldContain("HttpRequest.newBuilder(request.uri()).GET().build()");
    [Fact] void should_return_an_sse_stream() => _endpoint.ShouldContain("Flux<ServerSentEvent<Object>> stream(");
    [Fact] void should_subscribe_through_the_native_gateway() => _endpoint.ShouldContain("gateway.subscriptionQuery(query, LiveTotals.Result.class, metadata, null, Queues.SMALL_BUFFER_SIZE)");
    [Fact] void should_keep_the_latest_pending_update() => _endpoint.ShouldContain(".onBackpressureLatest()");
    [Fact] void should_refresh_with_the_callers_metadata() => _endpoint.ShouldContain("gateway.query(query, LiveTotals.Result.class, metadata, null)");
    [Fact] void should_start_heartbeats_after_the_first_result() => _endpoint.ShouldContain("data.switchOnFirst((first, values) -> first.hasValue()");
    [Fact] void should_end_at_the_configured_lifetime() => _endpoint.ShouldContain(".take(this.timeout)");
    [Fact] void should_bound_the_sse_merge_prefetch() => _endpoint.ShouldContain("Flux.merge(1, shared,");
    [Fact] void should_drop_heartbeats_without_demand() => _endpoint.ShouldContain("Flux.interval(this.heartbeat).onBackpressureDrop()");
    [Fact] void should_bound_the_shared_data_prefetch() => _endpoint.ShouldContain(".takeUntilOther(shared.ignoreElements())), 1)");
    [Fact] void should_not_send_an_open_frame() => _endpoint.ShouldNotContain("comment(\"open\")");
    [Fact] void should_document_the_missing_capacity_limit() => _endpoint.ShouldContain("// Differs from Screenplay: live stream capacity is not limited.");
    [Fact] void should_not_generate_a_subscription_helper() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/LiveTotalsSubscription.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_generate_a_streams_helper() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/LiveTotalsStreams.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_emit_ordered_rows_after_projecting_an_event() => Assert.True(_projector.Contains("repository.findAll(Sort.by(\"rowKey\")).stream().map(TotalEntity::toModel).toList()", StringComparison.Ordinal), _projector);
    [Fact] void should_not_invalidate_the_argumentless_query() => _projector.ShouldNotContain("Result::invalidation");
    [Fact] void should_resolve_updates_from_the_handler_context() => _projector.ShouldContain("event, QueryUpdateEmitter updates)");
    [Fact] void should_not_require_a_global_update_emitter() => _projector.ShouldNotContain("private final QueryUpdateEmitter");
    [Fact] void should_emit_an_initial_and_update_native_test() => Source("LiveTotalsSubscriptionTest.java").ShouldContain("shouldDeliverInitialSnapshotAndUpdateAndCancelAxonPublisher");
    [Fact] void should_serialize_expected_sse_values_with_the_application_concept_mapper() => Source("LiveTotalsSseTest.java").ShouldContain("var mapper = application.getBean(JsonMapper.class);");
    [Fact] void should_emit_an_http_sse_probe() => Source("LiveTotalsSseTest.java").ShouldContain("shouldDeliverInitialDataAndOneUpdateOverHttpSse");
    [Fact] void should_emit_a_five_second_shutdown_assertion() => Source("LiveTotalsSseTest.java").ShouldContain("elapsed.compareTo(Duration.ofSeconds(5)) < 0");
    [Fact] void should_leave_the_capacity_warning_out_of_the_unsupported_summary() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md")).ShouldNotContain("STAGE-AXON-LIVE-CAPACITY");
    [Fact] void should_report_one_capacity_warning_for_the_live_endpoint() => _context.Degradation.Warnings.Single(diagnostic => diagnostic.Artifact == QueryId("LiveTotals")).Code.ShouldEqual("STAGE-AXON-LIVE-CAPACITY");

    [Fact]
    void should_warn_without_refusing_live_capacity_in_strict_mode()
    {
        var plan = new AxonArtifactRenderPlanner().Plan(_request with { Profile = AxonRendering.CreateProfile(strict: true) });
        Assert.DoesNotContain(plan.Refusals, refusal => refusal.Codes.Contains("STAGE-AXON-LIVE-CAPACITY"));
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == "STAGE-AXON-LIVE-CAPACITY" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning);
    }

    string Source(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)));
}
