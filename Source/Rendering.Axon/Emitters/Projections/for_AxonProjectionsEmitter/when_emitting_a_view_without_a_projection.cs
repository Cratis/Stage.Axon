// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_view_without_a_projection : Specification
{
    AxonEmissionContext context;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/live-navigation/model");
        var compilation = await SemanticModelLoader.LoadAsync(root, [], null, "Navigation");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        context = new(request, new AxonDegradation(request.Profile));
    }

    void Because() => new AxonProjectionsEmitter().Emit(context);

    [Fact] void should_emit_a_repository_for_each_model() => context.Artifacts.Count(artifact => artifact.RelativePath.EndsWith("Repository.java", StringComparison.Ordinal)).ShouldBeGreaterThan(0);
    [Fact] void should_not_emit_store_configurations() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_warn_once_for_each_writerless_model() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(4);
    [Fact] void should_use_only_the_writerless_warning() => context.Degradation.UnsupportedConstructs.ShouldEachConformTo(diagnostic => diagnostic.Code == "STAGE-AXON-0309" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_explain_why_the_model_reads_empty() => context.Degradation.UnsupportedConstructs.ShouldEachConformTo(diagnostic => diagnostic.Message.Contains("always reads empty", StringComparison.Ordinal));
}
