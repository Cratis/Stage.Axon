// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_view_named_profile : Specification
{
    AxonEmissionContext context;
    string configuration;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Verification/blueprint-templates/model");
        var compilation = await SemanticModelLoader.LoadAsync(root, [], null, "Templates");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        context = new(request, new AxonDegradation(request.Profile));
    }

    void Because()
    {
        new AxonProjectionsEmitter().Emit(context);
        configuration = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ProfileStoreConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_type_the_store_with_the_model_rather_than_the_spring_annotation() => configuration.ShouldContain("ReadModelStore<Object, templates.customers.profile.managerprofile.Profile>");
    [Fact] void should_preserve_the_profile_hook() => configuration.ShouldContain("@Profile(\"!persistent\")");
    [Fact] void should_preserve_the_qualified_store_name() => configuration.ShouldContain("@Bean(\"templates.customers.profile.managerprofile.Profile\")");
}
