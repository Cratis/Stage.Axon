// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_view_without_a_projection_in_strict_mode : Specification
{
    AxonEmissionContext context;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/live-navigation/model");
        var compilation = await SemanticModelLoader.LoadAsync(root, [], null, "Navigation");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(strict: true), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        context = new(request, new AxonDegradation(request.Profile));
    }

    void Because() => new AxonProjectionsEmitter().Emit(context);

    [Fact] void should_complete_emission() => context.Artifacts.ShouldNotBeEmpty();
    [Fact] void should_refuse_each_writerless_model() => context.Degradation.Refusals.Length.ShouldEqual(4);
    [Fact] void should_refuse_only_for_the_writerless_code() => context.Degradation.Refusals.ShouldEachConformTo(refusal => refusal.Codes.SequenceEqual(new[] { "STAGE-AXON-0309" }));
    [Fact] void should_still_emit_the_store_configurations() => context.Artifacts.Count(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).ShouldEqual(4);
}
