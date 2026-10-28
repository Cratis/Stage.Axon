// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Scaffold;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_multi_segment_proof : Projections.for_AxonProjectionsEmitter.given.a_view_request
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

    [Fact] void should_emit_a_default_surefire_sibling() => Proof.ShouldContain("class TotalPersistentMultiSegmentTest");
    [Fact] void should_exercise_handler_failure() => Proof.ShouldContain("assertRollbackAndReclaim(Failure.HANDLER)");
    [Fact] void should_exercise_real_token_store_failure() => Proof.ShouldContain("assertRollbackAndReclaim(Failure.TOKEN)");
    [Fact] void should_exercise_off_thread_refusal() => Proof.ShouldContain("assertRollbackAndReclaim(Failure.ASYNC)");
    [Fact] void should_exercise_non_joined_refusal() => Proof.ShouldContain("assertRollbackAndReclaim(Failure.NON_JOINED)");
    [Fact] void should_start_both_boots_with_four_segments() => Proof.Split("startMultiSegment(path)", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_pin_four_segments_only_in_the_proof_boot() => Support.ShouldContain("\"--axon.eventhandling.processors[axonviews.views.counters.totals].initial-segment-count=4\"");
    [Fact] void should_wait_for_all_four_token_rows() => Support.ShouldContain("await(() -> tokens(application).size() == 4)");
    [Fact] void should_close_a_boot_that_fails_its_fixture_wait() => Support.ShouldContain("catch (Exception | Error cause) {\n            application.close();\n            throw cause;");
    [Fact]
    void should_observe_the_public_delivery_segment_before_writing()
    {
        var observation = Support.IndexOf("var owner = Segment.fromContext(context).map(Segment::getSegmentId)", StringComparison.Ordinal);
        observation.ShouldBeGreaterThan(0);
        observation.ShouldBeLessThan(Support.IndexOf("var bound = JpaReadModelStore.bind(store(application), context)", StringComparison.Ordinal));
        Support.ShouldContain("orElseThrow(() -> new AssertionError(\"FIXTURE: pooled witness requires an owning segment\"))");
    }
    [Fact] void should_remember_ownership_by_the_actual_event_tag() => Support.ShouldContain("observedOwners.put(tag(event), owner)");
    [Fact]
    void should_select_the_observed_owner_for_the_event_to_fail()
    {
        Proof.ShouldContain("import axonviews.views.events.Increased;");
        Proof.ShouldContain("var owner = observedOwners.get(tag(new Increased(");
    }
    [Fact] void should_require_an_observed_owner() => Proof.ShouldContain("assertNotNull(owner, \"FIXTURE: arrangement must observe the failed event's owning segment\")");
    [Fact] void should_require_exactly_four_checkpoint_keys() => Proof.ShouldContain("assertEquals(4, checkpoint.size(), \"FIXTURE: multi-segment proof requires four processor segments\")");
    [Fact] void should_require_the_owner_in_the_checkpoint() => Proof.ShouldContain("assertTrue(checkpoint.containsKey(owner), \"FIXTURE: observed owner must have a checkpoint\")");
    [Fact] void should_require_a_persisted_owner_token() => Proof.ShouldContain("assertNotEquals(\"<null>\", checkpoint.get(owner), \"FIXTURE: observed owner must have a persisted token\")");
    [Fact]
    void should_check_preconditions_before_injecting_failure()
    {
        var injection = Proof.IndexOf("failure = requested;", StringComparison.Ordinal);
        Proof.IndexOf("assertFixture(application, before);", StringComparison.Ordinal).ShouldBeLessThan(injection);
        Proof.IndexOf("assertTrue(checkpoint.containsKey(owner)", StringComparison.Ordinal).ShouldBeLessThan(injection);
        Proof.IndexOf("assertEquals(4, checkpoint.size()", StringComparison.Ordinal).ShouldBeLessThan(injection);
    }
    [Fact] void should_check_the_failed_delivery_owner() => Proof.ShouldContain("assertEquals(owner, observedOwner, \"FIXTURE: failed delivery must use the observed owner\")");
    [Fact] void should_scope_the_trigger_to_the_observed_owner() => Proof.ShouldContain("if (requested == Failure.TOKEN) installTokenFailure(path, owner)");
    [Fact] void should_read_the_trigger_segment_column() => Support.ShouldContain("if (name.equalsIgnoreCase(\"segment\")) segmentColumn = index - 1");
    [Fact] void should_filter_token_rejection_by_owner() => Support.ShouldContain("(tokenFailureSegment == null || tokenFailureSegment.equals(after[segmentColumn]))");
    [Fact] void should_keep_the_single_segment_trigger_unfiltered() => Support.ShouldContain("static void installTokenFailure(Path path) throws Exception { installTokenFailure(path, null); }");
    [Fact] void should_require_the_real_owner_scoped_token_rejection() => Proof.ShouldContain("assertTrue(tokenRejected.await(20, TimeUnit.SECONDS), \"Real JpaTokenStore update must reach the owner-scoped rejecting trigger\")");
    [Fact] void should_preserve_the_owners_raw_token() => Proof.ShouldContain("assertEquals(checkpoint.get(owner), after.get(owner), \"Failed owner checkpoint must remain byte-identical\")");
    [Fact] void should_preserve_the_segment_key_set() => Proof.ShouldContain("assertEquals(checkpoint.keySet(), after.keySet(), \"Failure must not change the segment key set\")");
    [Fact] void should_decode_persisted_tokens_with_the_public_converter() => Proof.ShouldContain("checkpoint.put(entry.getSegment().getSegmentId(), entry.getToken(converter))");
    [Fact] void should_compare_only_non_owners_for_monotonicity() => Proof.ShouldContain("if (segment.equals(owner)) continue;");
    [Fact] void should_compare_the_lower_token_bounds() => Proof.ShouldContain("assertCovers(WrappedToken.unwrapLowerBound(decodedAfter.get(segment)),\n                    WrappedToken.unwrapLowerBound(decodedCheckpoint.get(segment)), segment)");
    [Fact] void should_compare_the_upper_token_bounds() => Proof.ShouldContain("assertCovers(WrappedToken.unwrapUpperBound(decodedAfter.get(segment)),\n                    WrappedToken.unwrapUpperBound(decodedCheckpoint.get(segment)), segment)");
    [Fact] void should_require_non_owner_coverage() => Proof.ShouldContain("assertTrue(after.covers(before), \"Non-owner token must cover its prior position: \" + segment)");
    [Fact] void should_reject_regression_to_null() => Proof.ShouldContain("assertNotNull(after, \"Non-owner token must not regress to null: \" + segment)");
    [Fact] void should_accept_an_initial_null_bound() => Proof.ShouldContain("if (before == null) return;");
    [Fact] void should_require_counter_rollback() => Proof.ShouldContain("assertEquals(1, count(application), \"Non-idempotent view change must roll back\")");
    [Fact] void should_require_modeled_row_rollback() => Proof.ShouldContain("assertEquals(normalized(initial), normalized(store(application).all()), \"No independently committed projection write\")");
    [Fact] void should_require_exactly_once_reclaim() => Proof.ShouldContain("count(application) == 1 + 1");
    [Fact]
    void should_report_state_when_reclaim_times_out() => Proof.ShouldContain("""
                        await(() -> normalized(store(application).all()).equals(normalized(afterRetry)) && count(application) == 1 + 1,
                                () -> ": expected rows: " + normalized(afterRetry) + ", actual rows: " + normalized(store(application).all()) + ", count: " + count(application) + ", tokens: " + tokens(application));
        """);
    [Fact] void should_require_exactly_once_subsequent_delivery() => Proof.ShouldContain("count(application) == 1 + 2");
    [Fact] void should_query_reclaimed_durable_rows() => Proof.ShouldContain("assertQuery(application, afterRetry)");
    [Fact] void should_keep_the_whole_map_single_segment_control() => Code("TotalPersistentTest").ShouldContain("assertEquals(checkpoint, tokens(application), \"Failed processing must not advance the actual checkpoint\")");
    [Fact] void should_keep_the_single_segment_boot_pin() => Support.ShouldContain("\"--axon.eventhandling.processors[axonviews.views.counters.totals].initial-segment-count=1\"");
    [Fact] void should_point_the_control_at_its_sibling() => Code("TotalPersistentTest").ShouldContain("See the sibling TotalPersistentMultiSegmentTest for observed-owner multi-segment rollback coverage.");
    [Fact]
    void should_not_use_internal_or_recomputed_owner_resolution()
    {
        foreach (var source in new[] { Support, Proof })
        {
            source.ShouldNotContain("SegmentMatcher");
            source.ShouldNotContain("ProcessorEventHandlingComponents");
            source.ShouldNotContain(".resources()");
            source.ShouldNotContain("java.lang.reflect");
            source.ShouldNotContain("sequenceIdentifierFor");
            source.ShouldNotContain("owningSegment(");
            source.ShouldNotContain("EnabledIfSystemProperty");
        }
    }
    [Fact]
    void should_not_change_production_segment_defaults()
    {
        var production = _context.Artifacts.Where(artifact => artifact.RelativePath.StartsWith("src/main/", StringComparison.Ordinal)).ToArray();
        production.Length.ShouldBeGreaterThan(0);
        foreach (var artifact in production)
        {
            var source = Text(artifact);
            source.ShouldNotContain("initial-segment-count");
            source.ShouldNotContain("initialSegmentCount");
        }
    }

    [Fact] void should_emit_a_source_independent_last_writer_proof() => Code("TotalPersistentRowOrderTest").ShouldContain("assertNotEquals(normalized(reference.all()), normalized(inverted.all())");
    [Fact] void should_pause_only_the_old_source_segment() => Code("TotalPersistentRowOrderTest").ShouldContain("releaseSegment(held.getSegmentId(), 1, TimeUnit.DAYS)");
    [Fact] void should_wait_for_a_committed_competing_token_before_reclaim() => Code("TotalPersistentRowOrderTest").ShouldContain("coversHead(application, terminalHead, rowSegment.getSegmentId())");
    [Fact] void should_require_every_segment_to_cover_the_terminal_head() => Code("TotalPersistentRowOrderTest").ShouldContain("coversHead(application, terminalHead, null)");

    string Proof => Code("TotalPersistentMultiSegmentTest");
    string Support => Code("TotalPersistentSupport");

    string Code(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
