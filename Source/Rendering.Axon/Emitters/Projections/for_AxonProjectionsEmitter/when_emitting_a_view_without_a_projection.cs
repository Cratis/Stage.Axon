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
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Verification/live-navigation/model");
        var compilation = await SemanticModelLoader.LoadAsync(root, [], null, "Navigation");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        context = new(request, new AxonDegradation(request.Profile));
    }

    void Because() => new AxonProjectionsEmitter().Emit(context);

    [Fact] void should_emit_the_property_source_required_by_the_store_configuration() => context.Artifacts.Exists(artifact => artifact.RelativePath == "src/main/resources/axon-views.properties").ShouldBeTrue();
}
