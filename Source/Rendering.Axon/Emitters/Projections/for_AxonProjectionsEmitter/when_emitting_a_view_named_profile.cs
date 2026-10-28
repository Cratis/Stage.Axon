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
    string repository;
    string entity;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/blueprint-templates/model");
        var compilation = await SemanticModelLoader.LoadAsync(root, [], null, "Templates");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        context = new(request, new AxonDegradation(request.Profile));
    }

    void Because()
    {
        new AxonProjectionsEmitter().Emit(context);
        repository = Text("/ProfileRepository.java");
        entity = Text("/ProfileEntity.java");
    }

    string Text(string suffix) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith(suffix, StringComparison.Ordinal)).Bytes.AsSpan());

    [Fact] void should_type_the_repository_with_the_entity_of_the_model() => repository.ShouldContain("JpaRepository<ProfileEntity, String>");
    [Fact] void should_not_import_the_spring_annotation_over_the_model() => entity.ShouldNotContain("import org.springframework.context.annotation.Profile;");
    [Fact] void should_map_the_entity_back_to_the_model() => entity.ShouldContain("public Profile toModel()");
    [Fact] void should_keep_the_qualified_repository_bean_name() => repository.ShouldContain("@Repository(\"templates.customers.profile.managerprofile.ProfileRepository\")");
}
