// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Scaffold;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_guarded_flat_views : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonScaffoldEmitter().Emit(_context);
        new AxonProjectionsEmitter().Emit(_context);
        new AxonQueriesEmitter().Emit(_context);
        new AxonPersistenceEmitter().Emit(_context);
    }

    [Fact] void should_admit_the_flat_projection() => PersistentViewAdmission.Supports(_context, ReadModelId("Total")).ShouldBeTrue();
    [Fact] void should_refuse_scoped_projections() => PersistentViewAdmission.Supports(_context, ReadModelId("Board")).ShouldBeFalse();
    [Fact] void should_refuse_reducers() => PersistentViewAdmission.Supports(_context, ReadModelId("RebuiltTotal")).ShouldBeFalse();
    [Fact] void should_preserve_early_scoped_refusal() => Code("BoardStoreConfiguration").ShouldContain("public static PersistentReadModelSafety persistentReadModelSafety()");
    [Fact] void should_preserve_early_reducer_refusal() => Code("RebuiltTotalStoreConfiguration").ShouldContain("public static PersistentReadModelSafety persistentReadModelSafety()");
    [Fact] void should_remove_only_the_flat_guard() => Code("TotalStoreConfiguration").ShouldNotContain("persistentReadModelSafety");
    [Fact] void should_keep_the_direct_call_signature() => Code("TotalProjectionProjector").ShouldContain("public void on(Increased event, org.axonframework.messaging.queryhandling.QueryUpdateEmitter updates)");
    [Fact] void should_bind_the_actual_handler_context() => Code("TotalProjectionProjector").ShouldContain("JpaReadModelStore.bind(store, context)");
    [Fact] void should_request_the_handler_context() => Code("TotalProjectionProjector").ShouldContain("QueryUpdateEmitter updates, ProcessingContext context)");
    [Fact] void should_defer_live_notifications_until_commit() => Code("TotalProjectionProjector").ShouldContain("context.runOnAfterCommit");
    [Fact] void should_provide_only_qualified_persistent_stores() => Code("TotalStoreConfiguration").ShouldContain("return new JpaReadModelStore<>(factory, mapper, \"axonviews.views.counters.totals.Total\", Total.class)");
    [Fact] void should_depend_on_generation_admission() => Code("TotalStoreConfiguration").ShouldContain("PersistentViewConfiguration generation");
    [Fact] void should_pin_pooled_processing() => Code("PersistentViewConfiguration").ShouldContain("EventProcessorDefinition.pooledStreamingMatching(");
    [Fact] void should_pin_propagating_errors() => Code("PersistentViewConfiguration").ShouldContain("config.errorHandler(org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler.INSTANCE)");
    [Fact] void should_preserve_immediate_memory_notifications() => Code("TotalProjectionProjector").ShouldContain("if (!(store instanceof JpaReadModelStore<?>))");
    [Fact] void should_refuse_async_writes() => Code("JpaReadModelStore").ShouldContain("Thread.currentThread() != handlerThread");
    [Fact] void should_test_async_refusal() => Code("TotalPersistentStoreTest").ShouldContain("CompletableFuture.runAsync(() -> bound.put(key, value))");
    [Fact] void should_refuse_contextless_writes() => Code("JpaReadModelStore").ShouldContain("if (context == null) throw new IllegalStateException");
    [Fact] void should_never_create_an_independent_write_executor() => Code("JpaReadModelStore").ShouldContain("findTransactionalExecutor(context)");
    [Fact] void should_never_fall_back_to_an_executor() => Code("JpaReadModelStore").ShouldNotContain("getTransactionalExecutor(");
    [Fact] void should_serialize_row_payloads() => Code("JpaReadModelStore").ShouldContain("mapper.writeValueAsString(next)");
    [Fact] void should_guard_both_retained_events_and_checkpoints() => Code("PersistentViewBootstrap").ShouldContain("Stream.of(AggregateEventEntry.class, TokenEntry.class, ViewRow.class)");
    [Fact] void should_refuse_generation_changes() => Code("PersistentViewBootstrap").ShouldContain("!existing.fingerprint().equals(fingerprint)");
    [Fact] void should_emit_a_default_native_two_boot_test() => Code("TotalPersistentTest").ShouldContain("void handlerThrowsAfterViewWriteAndReclaimAppliesExactlyOnce()");
    [Fact] void should_exercise_handler_failure_after_view_write() => Code("TotalPersistentSupport").ShouldContain("deliberate handler failure after view write");
    [Fact] void should_exercise_real_token_store_failure() => Code("TotalPersistentSupport").ShouldContain("create trigger stage_token_failure before update on token_entry");
    [Fact] void should_test_non_joined_handler_transaction() => Code("TotalPersistentSupport").ShouldContain("context.withResource(JpaTransactionalExecutorProvider.SUPPLIER_KEY");
    [Fact] void should_test_off_thread_handler_refusal() => Code("TotalPersistentTest").ShouldContain("void actualHandlerRefusesOffThreadWriteWithoutChangingRowsOrToken()");
    [Fact] void should_validate_customized_processors_before_start() => Code("PersistentViewConfiguration").ShouldContain("SmartInitializingSingleton persistentViewProcessorGuard");
    [Fact] void should_refuse_swallowing_customizations() => Code("PersistentViewProcessorGuard").ShouldContain("processor.errorHandler() != PropagatingErrorHandler.INSTANCE");
    [Fact] void should_test_subscribing_replacement_refusal() => Code("TotalPersistentEnvelopeTest").ShouldContain("void refusesSubscribingReplacementBeforeReadiness()");
    [Fact] void should_reference_the_unexpected_context_in_the_unconditional_failure() => Code("TotalPersistentEnvelopeTest").ShouldContain("try (var application = start(database(), customization)) { fail(\"Unsafe processor reached readiness: active=\" + application.isActive()); }");
    [Fact] void should_keep_the_envelope_startup_exception_assertion() => Code("TotalPersistentEnvelopeTest").ShouldContain("var error = assertThrows(Exception.class, () -> {");
    [Fact] void should_keep_the_envelope_refusal_reason_assertion() => Code("TotalPersistentEnvelopeTest").ShouldContain("assertTrue(messages.toString().contains(\"Persistent flat views require pooled processors with PropagatingErrorHandler\"));");
    [Fact] void should_compare_the_whole_checkpoint_map_after_rollback() => Code("TotalPersistentTest").ShouldContain("assertEquals(checkpoint, tokens(application), \"Failed processing must not advance the actual checkpoint\")");
    [Fact] void should_pin_only_the_proof_processor_at_boot() => Code("TotalPersistentSupport").ShouldContain("\"--axon.eventhandling.processors[axonviews.views.counters.totals].initial-segment-count=1\"");
    [Fact] void should_create_a_fresh_database_for_each_proof() => Code("TotalPersistentSupport").ShouldContain("UUID.randomUUID().toString()");
    [Fact] void should_remove_owner_resolution() => Code("TotalPersistentTest").ShouldNotContain("owningSegment(");
    [Fact] void should_remove_segment_aware_rollback_comparisons() => Code("TotalPersistentTest").ShouldNotContain("assertRollbackTokens(");
    [Fact] void should_remove_token_decoding_helpers() => Code("TotalPersistentTest").ShouldNotContain("tokenEntries(");
    [Fact] void should_remove_non_owner_monotonicity_checks() => Code("TotalPersistentTest").ShouldNotContain("WrappedToken");
    [Fact] void should_document_single_segment_coverage() => Code("TotalPersistentTest").ShouldContain("Single-segment proof: native run 40395678690 showed public sequenceIdentifierFor + Segment.matches selected segment 2 while pooled routing used segment 13. Multi-segment rollback is tracked in Cratis/Stage.Axon#16; this proof does not cover it.");
    [Fact] void should_preserve_production_application_configuration() => Resource("application.yml").ShouldNotContain("initial-segment-count");
    [Fact] void should_preserve_production_view_configuration() => Resource("axon-views.properties").ShouldNotContain("initial-segment-count");
    [Fact] void should_preserve_production_persistent_profile_configuration() => Resource("application-persistent.properties").ShouldNotContain("initial-segment-count");
    [Fact] void should_preserve_production_pooled_definitions() => Code("PersistentViewConfiguration").ShouldNotContain("initialSegmentCount");
    [Fact]
    void should_require_one_segment_before_each_whole_checkpoint_comparison()
    {
        var proof = Code("TotalPersistentTest");
        const string precondition = "assertEquals(1, checkpoint.size(), \"Single-segment proof requires exactly one processor segment\");";
        var rollback = proof.IndexOf("assertEquals(checkpoint, tokens(application), \"Failed processing", StringComparison.Ordinal);
        var restart = proof.IndexOf("assertEquals(checkpoint, tokens(application), \"Retain the real checkpoint", StringComparison.Ordinal);
        proof[..restart].ShouldContain(precondition);
        proof[(restart + 1)..rollback].ShouldContain(precondition);
    }
    [Fact] void should_test_retained_database_refusal() => Code("TotalPersistentLegacyTest").ShouldContain("assertEquals(before, snapshot(path)");
    [Fact] void should_test_store_contracts() => Code("TotalPersistentStoreTest").ShouldContain("void durableStoreContractIsIsolatedAtomicAndFailsClosed()");
    [Fact] void should_source_the_same_tag_before_appending() => Code("TotalPersistentSupport").ShouldContain("eventStore.transaction(context).source(SourcingCondition.conditionFor(EventCriteria.havingTags(tag(event))))");
    [Fact] void should_consume_the_source_in_the_append_unit_of_work() => Code("TotalPersistentSupport").ShouldContain(".reduce(0, (count, entry) -> count + 1).thenApply(count -> {");
    [Fact] void should_emit_modeled_primary_tags() => Code("TotalPersistentFixture").ShouldContain("tagged.eventSourceId().toString()");
    [Fact] void should_read_sequences_without_writing() => Code("TotalPersistentFixture").ShouldContain("connection.setReadOnly(true)");
    [Fact] void should_require_contiguous_aggregate_sequences() => Code("TotalPersistentFixture").ShouldContain("FIXTURE: C1 aggregate sequences must be exactly 0..n-1");
    [Fact] void should_compare_all_sequences_to_the_append_count() => Code("TotalPersistentFixture").ShouldContain("assertEquals(java.util.stream.LongStream.range(0, count).boxed().toList(), sequences");
    [Fact] void should_require_the_actual_pooled_processor() => Code("TotalPersistentFixture").ShouldContain("assertInstanceOf(PooledStreamingEventProcessor.class, processor, \"FIXTURE: C2");
    [Fact] void should_require_token_progress_and_a_view_row() => Code("TotalPersistentFixture").ShouldContain("FIXTURE: C3 token must advance and modeled view row must exist before injection");
    [Fact]
    void should_check_fixture_before_injecting_failure()
    {
        var proof = Code("TotalPersistentTest");
        var injection = proof.IndexOf("failure = requested;", StringComparison.Ordinal);
        var fixture = proof.LastIndexOf("assertFixture(application, before);", injection, StringComparison.Ordinal);
        fixture.ShouldBeGreaterThan(0);
        fixture.ShouldBeLessThan(injection);
    }
    [Fact] void should_reject_each_missing_persistent_processor() => Code("PersistentViewProcessorGuard").ShouldContain("if (!(processors.get(name) instanceof PooledStreamingEventProcessor)) throw refusal();");
    [Fact] void should_reject_subscribing_persistent_processor_registrations() => Code("PersistentViewProcessorGuard").ShouldContain("otherProcessors.containsKey(name) && !(otherProcessors.get(name) instanceof PooledStreamingEventProcessor)");
    [Fact] void should_not_hide_native_proof_behind_a_property() => Code("TotalPersistentTest").ShouldNotContain("EnabledIfSystemProperty");

    string Code(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());

    string Resource(string name) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/" + name));
}
