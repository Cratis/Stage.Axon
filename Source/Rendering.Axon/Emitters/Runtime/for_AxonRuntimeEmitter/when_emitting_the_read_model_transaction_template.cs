// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_read_model_transaction_template : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _source;
    string _test;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
        _source = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ReadModelTransaction.java", StringComparison.Ordinal)).Bytes.AsSpan());
        _test = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ReadModelTransactionTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_apply_after_commit_before_after_commit() => _source.ShouldContain("DefaultPhases.AFTER_COMMIT.order() - 1");
    [Fact] void should_release_in_do_finally() => _source.ShouldContain("context.doFinally(completed -> changes.release())");
    [Fact] void should_apply_in_the_phase_before_after_commit() => _source.ShouldContain("context.runOn(APPLY, completed -> changes.commit())");
    [Fact] void should_share_changes_within_a_context() => _source.ShouldContain("context.computeResourceIfAbsent(coordinator.<K, V>resource()");
    [Fact] void should_reserve_keys_across_contexts() => _source.ShouldContain("reservations.get(key) != owner");
    [Fact] void should_bound_the_reservation_wait() => _source.ShouldContain("TimeUnit.SECONDS.toNanos(10)");
    [Fact] void should_refuse_contextless_binding() => _source.ShouldContain("Objects.requireNonNull(context,");
    [Fact] void should_not_carry_a_license_header() => _source.ShouldNotContain("Copyright");
    [Fact] void should_use_store_identity() => _source.ShouldContain("get() == key.get()");
    [Fact] void should_release_collected_stores() => _source.ShouldContain("COORDINATORS.remove(key)");
    [Fact] void should_apply_through_the_wrapped_store() => _source.ShouldContain("store.put(key, value.get())");
    [Fact] void should_restore_the_interrupt_flag() => _source.ShouldContain("Thread.currentThread().interrupt()");
    [Fact] void should_emit_native_rollback_proof() => _test.ShouldContain("discardsFailedWritesAndRetainsCommittedRows");
    [Fact] void should_emit_native_wrapper_ordering_proof() => _test.ShouldContain("publishesCommittedWrapperChangesBeforeAfterCommitCallbacks");
    [Fact] void should_emit_native_concurrent_rollback_proof() => _test.ShouldContain("doesNotLetOneRollbackEraseAnotherCommittedUnitOfWork");
    [Fact] void should_emit_native_timeout_failure_proof() => _test.ShouldContain("failsTheUnitOfWorkVisiblyWhenReservationTimesOut");
    [Fact] void should_emit_native_two_key_lock_order_proof() => _test.ShouldContain("serializesTwoKeyUpdatesAcquiredInSortedOrderWithoutDeadlock");
    [Fact] void should_expose_batch_reservation() => _source.ShouldContain("public static void reserve(ReadModelStore<?, ?> boundStore, Collection<?> keys)");
    [Fact] void should_sort_and_deduplicate_batch_keys() => _source.ShouldContain("keys.stream().distinct().sorted(ReadModelTransaction::compareKeys)");
    [Fact] void should_use_natural_order_for_comparable_keys() => _source.ShouldContain("left instanceof Comparable comparable) return comparable.compareTo(right)");
    [Fact] void should_share_an_owner_across_stores_in_a_context() => _source.ShouldContain("context.computeResourceIfAbsent(OWNER, Object::new)");
    [Fact] void should_coordinate_all_stores_on_one_monitor() => _source.ShouldContain("TimeUnit.NANOSECONDS.timedWait(MONITOR, remaining)");
    [Fact] void should_detect_cycles() => _source.ShouldContain("Read model keys are locked in a cycle with another unit of work");
    [Fact] void should_resolve_waits_against_current_key_ownership() => _source.ShouldContain("reservation.coordinator.reservations.get(reservation.key)");
    [Fact] void should_release_immediately_after_applying_writes() => _source.ShouldContain("            });\n            release();");
    [Fact] void should_emit_native_opposite_order_proof() => _test.ShouldContain("doesNotDeadlockWhenTwoUnitsOfWorkTouchTheSameKeysInOppositeOrder");
    [Fact] void should_emit_native_cross_store_cycle_proof() => _test.ShouldContain("failsFastOnAReservationCycleAcrossStores");
    [Fact] void should_emit_native_early_release_proof() => _test.ShouldContain("releasesReservationsBeforeAfterCommitCallbacks");

    [Fact]
    void should_use_imports_not_qualified_names() =>
        _source.Split('\n').Any(line => !line.StartsWith("import ", StringComparison.Ordinal) && line.Contains("org.axonframework.", StringComparison.Ordinal)).ShouldBeFalse();
}
