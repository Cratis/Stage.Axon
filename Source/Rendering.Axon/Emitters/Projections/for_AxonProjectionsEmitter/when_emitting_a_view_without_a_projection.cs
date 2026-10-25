// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
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

    [Fact] void should_guard_every_model_only_configuration() => context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())).ShouldEachConformTo(configuration => configuration.Contains(".persistentReadModelSafety\")", StringComparison.Ordinal));
    [Fact] void should_have_model_only_configurations_to_guard() => context.Artifacts.Count(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).ShouldBeGreaterThan(0);
    [Fact] void should_emit_the_property_source_required_by_the_store_configuration() => context.Artifacts.Exists(artifact => artifact.RelativePath == "src/main/resources/axon-views.properties").ShouldBeTrue();
}
