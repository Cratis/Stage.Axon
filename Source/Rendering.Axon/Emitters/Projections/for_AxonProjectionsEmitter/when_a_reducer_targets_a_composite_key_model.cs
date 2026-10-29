// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_a_reducer_targets_a_composite_key_model : given.a_view_request
{
    AxonEmissionContext _context;
    string _specifications;
    string _reducer;

    void Because()
    {
        var original = ScopeSelection.Select(_request).Single(owner => !owner.Slice.Reducers.IsEmpty);
        var model = original.Slice.ReadModels.Single();
        model = model with { Properties = [.. model.Properties.Select(property => property with { IsIdentifier = true })] };

        // The ESM validator rejects composite reducers; exercise the renderer's admission boundary directly.
        var located = original with { Slice = original.Slice with { ReadModels = [model] } };
        _context = new(_request, new(_request.Profile));
        var types = new ViewJavaTypes(_request.Model.Application, JavaNames.PackageSegment(_request.Model.Application.Name));
        ReadModelEntities.Emit(_context, types, types.Package(located), model);
        new ReducerProjection(_context, types).Emit(located, model, located.Slice.Reducers.Single());
        ReducerViewSpecifications.Emit(_context, types, located, model, [.. located.Slice.Specifications]);
        _reducer = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducer.java", StringComparison.Ordinal)));
        _specifications = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_keep_the_embedded_id_entity() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalEntity.java", StringComparison.Ordinal))).ShouldContain("private RebuiltTotalKey rowKey;");
    [Fact] void should_emit_a_handler_free_0305_reducer_stub() => _reducer.ShouldContain("STAGE-AXON-0305");
    [Fact] void should_not_emit_handlers_for_the_incompatible_key() => _reducer.ShouldNotContain("@EventHandler");
    [Fact] void should_not_emit_string_keyed_repository_calls() => _reducer.ShouldNotContain("repository.");
    [Fact] void should_not_emit_string_keyed_entity_construction() => _reducer.ShouldNotContain("new RebuiltTotalEntity(");
    [Fact] void should_render_the_0305_specification_stub() => _specifications.ShouldContain("@Disabled(\"STAGE-AXON-0305:");
    [Fact] void should_not_emit_string_keyed_reads() => _specifications.ShouldNotContain("repository.findById(");
    [Fact] void should_not_emit_string_keyed_deletes() => _specifications.ShouldNotContain("repository.deleteById(");
    [Fact] void should_not_emit_string_keyed_writes() => _specifications.ShouldNotContain("new RebuiltTotalEntity(");
    [Fact] void should_report_a_0305_warning() => _context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0305" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
    [Fact] void should_not_refuse_in_default_mode() => _context.Degradation.Refusals.IsEmpty.ShouldBeTrue();
    [Fact] void should_not_consume_the_incompatible_reducer_bodies() => _context.ConsumedImplementations.Count.ShouldEqual(0);
}
