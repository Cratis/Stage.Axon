// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_a_projection_removes_rows : given.a_view_request
{
    string _projector;

    void Because()
    {
        _projector = Projector();
    }

    [Fact] void should_render_a_real_projector() => _projector.ShouldContain("repository.save(new TotalEntity(key, state));");
    [Fact] void should_not_degrade_to_the_scoped_stub() => _projector.ShouldNotContain("STAGE-AXON-0310");
    [Fact] void should_handle_the_removal_event() => _projector.ShouldContain("handle(ContextRecorded event");
    [Fact] void should_delete_the_row_for_the_removal_key() => _projector.ShouldContain("repository.deleteAllByIdInBatch(List.of(String.valueOf(event.");

    string Projector()
    {
        var removed = ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Name == "ContextRecorded").Id;
        var removal = new SemanticProjectionRemoval(removed, new SemanticProjectionValueKey(SemanticProjectionValue.EventSourceIdentity), null);
        SemanticFeature Feature(SemanticFeature feature) => feature with { Slices = [.. feature.Slices.Select(Slice)], Features = [.. feature.Features.Select(Feature)] };
        SemanticSlice Slice(SemanticSlice slice) => slice with
        {
            Projections = [.. slice.Projections.Select(candidate => candidate.Name == "TotalProjection" ? candidate with { Scope = candidate.Scope! with { Removals = [removal] } } : candidate)]
        };
        var application = _request.Model.Application with { Modules = [.. _request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application);
        var request = _request with { Model = model };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        return Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalProjectionProjector.java", StringComparison.Ordinal)));
    }
}
