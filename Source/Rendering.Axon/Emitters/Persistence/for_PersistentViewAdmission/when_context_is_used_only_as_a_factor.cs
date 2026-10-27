// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentViewAdmission;

public class when_context_is_used_only_as_a_factor : given.a_flat_view
{
    AxonEmissionContext _context;
    SemanticProjection _projection;

    void Because()
    {
        _request = _request with { Model = ExecutableSemanticModel.Create(new(29, 0), SemanticVersion.V29, _request.Model.Application) };
        _context = Render(slice => slice with
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
        _projection = ScopeSelection.Select(_context.Request).SelectMany(owner => owner.Slice.Projections).Single(projection => projection.ReadModel == ReadModelId("Total"));
    }

    [Fact] void should_keep_the_existing_flat_handler_classification() => ProjectionHandlerShape.Scoped(_projection).ShouldBeFalse();
    [Fact] void should_leave_the_numeric_context_path_unsupported() => PersistentViewAdmission.Context(new("sequenceNumber")).ShouldBeFalse();
    [Fact] void should_refuse_persistence_without_a_message_parameter() => PersistentViewAdmission.Supports(_context, ReadModelId("Total")).ShouldBeFalse();
    [Fact] void should_warn_without_crashing() => Code(_context, "TotalStoreConfiguration").ShouldContain("STAGE-AXON-0308");
}
