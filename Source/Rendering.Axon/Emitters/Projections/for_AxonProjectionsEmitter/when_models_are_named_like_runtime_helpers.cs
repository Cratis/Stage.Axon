// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_models_are_named_like_runtime_helpers : given.a_view_request
{
    readonly Dictionary<string, string> _entities = [];
    readonly Dictionary<string, string> _repositories = [];
    readonly Dictionary<string, string> _projectors = [];
    string _reducerProjector;

    void Because()
    {
        foreach (var name in new[] { "Entity", "Repository", "JpaRepository", "Id", "Column" })
        {
            SemanticFeature Feature(SemanticFeature feature) => feature with
            {
                Features = [.. feature.Features.Select(Feature)],
                Slices = [.. feature.Slices.Select(slice => slice with { ReadModels = [.. slice.ReadModels.Select(model => (model.Name == "Total" || model.Name == "RebuiltTotal") ? model with { Name = name } : model)] })]
            };
            var application = _request.Model.Application;
            var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
            var context = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
            new AxonProjectionsEmitter().Emit(context);
            _entities[name] = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + "Entity.java", StringComparison.Ordinal) && artifact.RelativePath.Contains("/counters/totals/", StringComparison.Ordinal)));
            _repositories[name] = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + "Repository.java", StringComparison.Ordinal) && artifact.RelativePath.Contains("/counters/totals/", StringComparison.Ordinal)));
            _projectors[name] = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalProjectionProjector.java", StringComparison.Ordinal)));
            if (name == "Repository") _reducerProjector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RepositoryReducerProjector.java", StringComparison.Ordinal)));
        }
    }

    [Fact] void should_qualify_the_colliding_entity_annotation() => _entities["Entity"].ShouldContain("@jakarta.persistence.Entity(name = \"axonviews.views.counters.totals.EntityEntity\")");
    [Fact] void should_not_import_the_colliding_entity_annotation() => _entities["Entity"].ShouldNotContain("import jakarta.persistence.Entity;");
    [Fact] void should_qualify_the_colliding_id_annotation() => _entities["Id"].ShouldContain("@jakarta.persistence.Id");
    [Fact] void should_qualify_the_colliding_column_annotation() => _entities["Column"].ShouldContain("@jakarta.persistence.Column(name = \"row_key\")");
    [Fact] void should_keep_the_stereotype_on_a_model_named_repository() => _repositories["Repository"].ShouldContain("@Repository(\"axonviews.views.counters.totals.RepositoryRepository\")");
    [Fact] void should_type_the_repository_with_the_entity_of_a_model_named_repository() => _repositories["Repository"].ShouldContain("JpaRepository<RepositoryEntity, String>");
    [Fact] void should_keep_the_jpa_interface_for_a_model_named_jpa_repository() => _repositories["JpaRepository"].ShouldContain("JpaRepository<JpaRepositoryEntity, String>");
    [Fact] void should_inject_the_repository_into_the_projector() => _projectors["Repository"].ShouldContain("private final RepositoryRepository repository;");
    [Fact] void should_inject_the_repository_into_the_reducer_projector() => _reducerProjector.ShouldContain("RepositoryRepository repository, RepositoryReducer reducer)");
    [Fact] void should_not_reference_the_removed_runtime_store() => _projectors.Values.Append(_reducerProjector).ShouldEachConformTo(code => !code.Contains("ReadModelStore", StringComparison.Ordinal));
}
