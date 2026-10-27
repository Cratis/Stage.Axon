// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewAdmission;

public class when_persisting_a_reducer_only_model : given.a_flat_view
{
    AxonEmissionContext _placed;
    AxonEmissionContext _unplaced;
    AxonEmissionContext _mixed;
    LocatedSemanticSlice _owner;

    void Because()
    {
        _owner = ScopeSelection.Select(_request).Single(owner => owner.Slice.Reducers.Any());
        _placed = Render(slice => slice);
        var template = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Projections).First(projection => projection.ReadModel == ReadModelId("Total"));
        _mixed = Render(slice => slice with
        {
            Projections = slice.ReadModels.Any(model => model.Name == "RebuiltTotal") ? [.. slice.Projections, template with
            {
                Id = SemanticId.Create(SemanticKind.Projection, "Reduction.Counters.Rebuilt.MixedProjection"),
                Name = "MixedProjection",
                ReadModel = ReadModelId("RebuiltTotal"),
                Scope = template.Scope! with { From = [template.Scope.From[0] with { EventContract = _owner.Slice.Reducers[0].Transitions[0].EventContract, Key = new SemanticProjectionValueKey(new SemanticProjectionEventSourceIdentity()), Mappings = [] }] }
            }] : slice.Projections
        });
        _request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        _unplaced = Render(slice => slice);
    }

    [Fact] void should_admit_fully_placed_transitions() => PersistentViewAdmission.Supports(_placed, ReadModelId("RebuiltTotal")).ShouldBeTrue();
    [Fact] void should_refuse_unplaced_transitions() => PersistentViewAdmission.Supports(_unplaced, ReadModelId("RebuiltTotal")).ShouldBeFalse();
    [Fact] void should_warn_for_unplaced_persistence() => Code(_unplaced, "RebuiltTotalStoreConfiguration").ShouldContain("STAGE-AXON-0308");
    [Fact] void should_keep_unplaced_transition_diagnostics() => _unplaced.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0306").ShouldBeTrue();
    [Fact] void should_refuse_mixed_writers() => PersistentViewAdmission.Supports(_mixed, ReadModelId("RebuiltTotal")).ShouldBeFalse();
    [Fact] void should_guard_mixed_writers_without_crashing() => Code(_mixed, "RebuiltTotalStoreConfiguration").ShouldContain("STAGE-AXON-0308");
    [Fact] void should_refuse_event_filters() => PersistentViewAdmission.Placed(_placed, _owner, _owner.Slice.Reducers[0] with { From = new(_owner.Slice.Id) }).ShouldBeFalse();
    [Fact] void should_bind_the_handler_store() => Projector.ShouldContain("JpaReadModelStore.bind(store, context)");
    [Fact] void should_register_the_reduction_processor_namespace() => Code(_placed, "PersistentViewConfiguration").ShouldContain("\"axonviews.reduction.counters.rebuilt\"");
    [Fact] void should_dispatch_in_the_slice_namespace() => Projector.ShouldContain("@Namespace(\"axonviews.reduction.counters.rebuilt\")");
    [Fact] void should_keep_the_memory_path_synchronized() => Projector.ShouldContain("public synchronized void on(CountRecorded event, EventMessage message");
    [Fact] void should_use_the_bound_store_for_deletion() => Projector.ShouldContain("if (next == null) store.remove(key); else store.put(key, next);");
    [Fact] void should_not_emit_notifications_without_observable_queries() => Projector.ShouldNotContain("runOnAfterCommit");
    [Fact] void should_construct_the_reducer_in_expected_rows() => Code(_placed, "RebuiltTotalPersistentSupport").ShouldContain("ReducerProjector(store, new axonviews.reduction.counters.rebuilt.RebuiltTotalReducer() {})");
    [Fact] void should_import_the_reducer_configuration() => Code(_placed, "RebuiltTotalPersistentSupport").ShouldContain("RebuiltTotalReducerConfiguration.class");
    [Fact] void should_emit_an_enabled_recovery_proof() => Code(_placed, "RebuiltTotalPersistentTest").ShouldNotContain("@Disabled");
    [Fact] void should_emit_an_enabled_multisegment_proof() => Code(_placed, "RebuiltTotalPersistentMultiSegmentTest").ShouldNotContain("@Disabled");
    [Fact] void should_replace_every_proof_placeholder() => _placed.Artifacts.Where(artifact => artifact.RelativePath.Contains("RebuiltTotalPersistent", StringComparison.Ordinal)).All(artifact => !Text(artifact).Contains("__", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_emit_a_proof_for_unplaced_reducers() => _unplaced.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("RebuiltTotalPersistentTest.java", StringComparison.Ordinal)).ShouldBeFalse();

    string Projector => Code(_placed, "RebuiltTotalReducerProjector");
}
