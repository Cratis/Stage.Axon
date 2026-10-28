// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_emitting_part_key_removal_proof : for_PersistentViewAdmission.given.a_flat_view
{
    string _proof;
    string _support;
    string _contract;

    void Because()
    {
        var context = Render(slice => slice);
        _proof = Code(context, "MetricPersistentRemovalTest");
        _support = Code(context, "MetricPersistentRemovalSupport");
        _contract = Code(context, "MetricPersistentRemovalStoreTest");
    }

    [Fact] void should_run_in_default_surefire() => _proof.ShouldContain("class MetricPersistentRemovalTest");
    [Fact] void should_cover_one_segment() => _proof.ShouldContain("lifecycle(1)");
    [Fact] void should_cover_four_segments() => _proof.ShouldContain("lifecycle(4)");
    [Fact] void should_cover_handler_rollback() => _proof.ShouldContain("rollback(4, false)");
    [Fact] void should_cover_real_token_rollback() => _proof.ShouldContain("rollback(4, true)");
    [Fact] void should_flush_the_actual_delete_before_injection() => _proof.ShouldContain("projector(bound).on(event, null)");
    [Fact] void should_hold_the_earlier_create() => _proof.ShouldContain("entered.await(10, TimeUnit.SECONDS)");
    [Fact] void should_compare_claimed_owners() => _proof.ShouldContain("claims(application)");
    [Fact] void should_read_claims_from_typed_token_entries() => _proof.ShouldContain("result.put(entry.getSegment().getSegmentId(), entry.getOwner())");
    [Fact] void should_not_cast_the_persisted_segment_id_to_a_segment() => _proof.ShouldNotContain("((Segment) row[0])");
    [Fact] void should_check_the_owning_checkpoint() => _proof.ShouldContain("decoded(checkpoint.get(owner))");
    [Fact] void should_prove_the_other_row_survives() => _proof.ShouldContain("Other row must survive rollback and reclaim");
    [Fact] void should_prove_recreation() => _proof.ShouldContain("Recreation is a create, not a removal replay");
    [Fact] void should_keep_legacy_removal_coverage() => _proof.ShouldContain("legacyProjectorStillRemovesOnlyTheSelectedRow");
    [Fact] void should_target_the_real_token_store() => _support.ShouldContain("create trigger stage_token_failure before update on token_entry");
    [Fact] void should_not_require_a_query_handler() => _support.ShouldNotContain("FindMetricHandler.class");
    [Fact] void should_cover_entry_order_at_the_store_boundary() => _contract.ShouldContain("partKeyIdentityIsIndependentOfEntryOrderForEveryStoreOperation");
    [Fact]
    void should_use_the_projector_part_names()
    {
        var projection = ScopeSelection.Locate(_request.Model.Application).SelectMany(owner => owner.Slice.Projections).Single(projection => projection.ReadModel == ReadModelId("Metric"));
        foreach (var part in ((Cratis.Screenplay.Semantics.SemanticProjectionPartsKey)projection.Scope!.From[0].Key).Parts) _proof.ShouldContain(part.Property.ToString());
    }

    [Fact] void should_force_distinct_default_segments() => _proof.ShouldContain("processor.releaseSegment(heldSegment.getSegmentId()");
    [Fact] void should_use_committed_owner_positions() => _proof.ShouldContain("reachedPosition(WrappedToken.unwrapLowerBound(token), head)");
    [Fact] void should_not_wait_for_gap_cleanup() => _proof.ShouldNotContain("token.covers(head)");
    [Fact] void should_check_non_owner_non_regression() => _proof.ShouldContain("Non-owner checkpoint must cover its prior committed position");
    [Fact] void should_reboot_after_recreation() => _proof.ShouldContain("Caught-up restart must never redeliver removal after recreation");
    [Fact] void should_pin_the_non_map_identity() => _contract.ShouldContain("Key.class.getName()");
    [Fact] void should_reject_type_confusion() => _contract.ShouldContain("Map.of(1, \"x\")");
    [Fact] void should_stringify_part_values_like_the_projector() => _proof.ShouldContain("String.valueOf(2L)");
    [Fact] void should_not_reference_test_fields_from_support() => _support.ShouldNotContain("new EventSourceId(createSource)");
    [Fact] void should_resolve_all_native_placeholders() => (_proof + _support + _contract).ShouldNotContain("__");
}
