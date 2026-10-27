// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_rollback_safe_default_views : Persistence.for_PersistentViewAdmission.given.a_flat_view
{
    AxonEmissionContext _admitted;
    AxonEmissionContext _nonAdmitted;

    void Because()
    {
        _admitted = Render(slice => slice);
        _request = _request with { Model = ExecutableSemanticModel.Create(new(29, 0), SemanticVersion.V29, _request.Model.Application) };
        _nonAdmitted = Render(slice => slice with
        {
            Projections = [.. slice.Projections.Select(projection => projection.ReadModel == ReadModelId("Total") ? projection with
            {
                Scope = projection.Scope! with
                {
                    From = [projection.Scope.From[0] with
                    {
                        Mappings = [.. projection.Scope.From[0].Mappings.Select(mapping => mapping.Operation == SemanticProjectionOperation.Add ? mapping with { Factor = new SemanticProjectionEventContextValue("sequenceNumber") } : mapping)]
                    }]
                }
            } : projection)]
        });
    }

    [Fact] void should_bind_default_flat_writes() => Code(_admitted, "TotalProjectionProjector").ShouldContain("else apply(event, updates, ReadModelTransaction.bind(store, context));");
    [Fact] void should_defer_persistent_invalidations_only_in_the_persistent_branch() => Code(_admitted, "TotalProjectionProjector").ShouldContain("if (store instanceof JpaReadModelStore<?>) {\n            apply(event, null, JpaReadModelStore.bind(store, context));\n            context.runOnAfterCommit(ignored ->");
    [Fact] void should_let_the_bound_emitter_defer_non_admitted_invalidations() => Code(_nonAdmitted, "TotalProjectionProjector").ShouldNotContain("context.runOnAfterCommit");
    [Fact] void should_import_the_transaction() => Code(_admitted, "TotalProjectionProjector").ShouldContain("import axonviews.runtime.ReadModelTransaction;");
    [Fact] void should_not_dispatch_through_the_unbound_method() => Code(_admitted, "TotalProjectionProjector").ShouldNotContain("on(event");
    [Fact] void should_keep_the_direct_call_unannotated() => Code(_admitted, "TotalProjectionProjector").ShouldContain("}\n    public void on(Increased event, QueryUpdateEmitter updates)");
    [Fact] void should_exercise_a_non_admitted_flat_view() => PersistentViewAdmission.Supports(_nonAdmitted, ReadModelId("Total")).ShouldBeFalse();
    [Fact] void should_use_the_proven_contextual_handler_for_non_admitted_views() => Code(_nonAdmitted, "TotalProjectionProjector").ShouldContain("@EventHandler\n    public void handle(Increased event, QueryUpdateEmitter updates, ProcessingContext context)");
    [Fact] void should_bind_non_admitted_default_writes() => Code(_nonAdmitted, "TotalProjectionProjector").ShouldContain("apply(event, updates, ReadModelTransaction.bind(store, context));");
    [Fact] void should_not_reference_absent_persistence() => Code(_nonAdmitted, "TotalProjectionProjector").ShouldNotContain("JpaReadModelStore");
    [Fact] void should_not_add_an_event_message_handler_parameter() => Code(_nonAdmitted, "TotalProjectionProjector").ShouldNotContain("EventMessage");
    [Fact] void should_document_scoped_rollback_divergence() => Code(_admitted, "BoardProjectionProjector").ShouldContain("default-profile writes are not yet rolled back with a failed unit of work");
    [Fact] void should_document_reducer_rollback_divergence() => Code(_admitted, "RebuiltTotalReducerProjector").ShouldContain("default-profile writes are not yet rolled back with a failed unit of work");
}
