// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_serializable_scope_state : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    AxonEmissionContext _context;
    string _projector;
    string _proof;

    void Because()
    {
        _context = Render(slice => slice);
        _projector = Code(_context, "BoardProjectionProjector");
        _proof = Code(_context, "BoardProjectionProjectorSerializationTest");
    }

    [Fact] void should_expose_a_data_snapshot() => _projector.ShouldContain("public record ScopeState(");
    [Fact] void should_not_store_predicates() => _projector.ShouldNotContain("Predicate");
    [Fact] void should_not_store_join_operators() => _projector.ShouldNotContain("UnaryOperator");
    [Fact] void should_whitelist_logical_event_kinds() => _projector.ShouldContain("JsonTypeInfo.Id.NAME");
    [Fact] void should_not_enable_class_name_typing() => _projector.ShouldNotContain("JsonTypeInfo.Id.CLASS");
    [Fact] void should_capture_original_message_identity() => _projector.ShouldContain("message.identifier(), message.timestamp(), message.metadata()");
    [Fact] void should_keep_null_message_semantics() => _projector.ShouldContain("if (absent) return null;");
    [Fact] void should_copy_ordered_maps() => _projector.ShouldContain("Collections.unmodifiableMap(new LinkedHashMap<>(");
    [Fact] void should_copy_pending_entries() => _projector.ShouldContain("pending = List.copyOf(pending);");
    [Fact] void should_dispatch_deferred_data() => _projector.ShouldContain("applyDeferred(iterator.next(), store)");
    [Fact] void should_store_typed_pending_events() => _projector.ShouldContain("pending.add(new DeferredCardAdded(event, StoredScopeMessage.capture(message)))");
    [Fact] void should_store_typed_join_events() => _projector.ShouldContain("new JoinRootChild00(event, StoredScopeMessage.capture(message))");
    [Fact] void should_reserve_scope_before_row_binding() => _projector.IndexOf("ReadModelTransaction.reserve(state, List.of(\"scope\"));", StringComparison.Ordinal).ShouldBeLessThan(_projector.IndexOf("ReadModelTransaction.bind(store, context)", StringComparison.Ordinal));
    [Fact] void should_keep_board_admission_closed() => PersistentViewAdmission.Supports(_context, ReadModelId("Board")).ShouldBeFalse();
    [Fact] void should_round_trip_concepts_with_the_application_mapper() => _proof.ShouldContain("TestSupport.mapper()");
    [Fact] void should_decode_pending_and_join_state() => _proof.ShouldContain("roundTripsPendingAndJoinWithoutReplayingHistory");
    [Fact] void should_reject_unknown_kinds() => _proof.ShouldContain("unknown-scope-kind");
    [Fact] void should_prove_original_metadata_and_time() => _proof.ShouldContain("assertMessage(pendingMessage, child)");
    [Fact] void should_prove_null_message_and_defensive_copy_semantics() => _proof.ShouldContain("snapshotsCopyCollectionsInOrderAndRetainNullMessages");
    [Fact] void should_continue_on_a_fresh_projector_without_replaying() => _proof.ShouldContain("scope(recovered).put(\"scope\", decoded)");
    [Fact] void should_cover_join_then_child_then_parent() => _proof.ShouldContain("childAndJoinBeforeParentSurvivesRoundTrips");
    [Fact] void should_cover_root_removal() => _proof.ShouldContain("removingTheRootSurvivesRoundTrips");
    [Fact] void should_cover_join_removal() => _proof.ShouldContain("removingViaJoinSurvivesRoundTrips");
    [Fact] void should_cover_nested_removal() => _proof.ShouldContain("removingThroughNestedScopeSurvivesRoundTrips");
    [Fact] void should_not_leave_placeholders() => _proof.ShouldNotContain("__");
    [Fact] void should_not_disable_the_proof() => _proof.ShouldNotContain("@Disabled");
    [Fact] void should_not_emit_a_license_header() => _proof.ShouldNotContain("Copyright (c) Cratis");
    [Fact] void should_emit_the_serialization_proof() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/BoardProjectionProjectorSerializationTest.java", StringComparison.Ordinal)).ShouldBeTrue();
}
