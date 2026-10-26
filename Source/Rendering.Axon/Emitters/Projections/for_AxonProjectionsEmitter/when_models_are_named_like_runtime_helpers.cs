// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_models_are_named_like_runtime_helpers : given.a_view_request
{
    readonly Dictionary<string, string> _configurations = [];
    string _projector;
    string _scopedProjector;
    string _reducerProjector;

    void Because()
    {
        foreach (var name in new[] { "ReadModelStore", "InMemoryReadModelStore", "PersistentReadModelSafety" })
        {
            SemanticFeature Feature(SemanticFeature feature) => feature with
            {
                Features = [.. feature.Features.Select(Feature)],
                Slices = [.. feature.Slices.Select(slice => slice with { ReadModels = [.. slice.ReadModels.Select(model => (model.Name == "Total" || model.Name == "Board" || model.Name == "RebuiltTotal") ? model with { Name = name } : model)] })]
            };
            var application = _request.Model.Application;
            var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
            var context = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
            new AxonProjectionsEmitter().Emit(context);
            var owner = Sdk.ScopeSelection.Select(_request).Single(slice => slice.Slice.ReadModels.Any(model => model.Name == "Total"));
            var package = new ViewJavaTypes(application).Package(owner).Replace('.', '/');
            _configurations[name] = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith(package + "/" + name + "StoreConfiguration.java", StringComparison.Ordinal)));
            if (name == "ReadModelStore")
            {
                _projector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalProjectionProjector.java", StringComparison.Ordinal)));
                _scopedProjector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BoardProjectionProjector.java", StringComparison.Ordinal)));
                _reducerProjector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ReadModelStoreReducerProjector.java", StringComparison.Ordinal)));
            }
        }
    }

    [Fact] void should_qualify_the_colliding_store_interface() => _configurations["ReadModelStore"].ShouldContain("public axonviews.runtime.ReadModelStore<Object, ReadModelStore> store()");
    [Fact] void should_qualify_the_colliding_store_implementation() => _configurations["InMemoryReadModelStore"].ShouldContain("return new axonviews.runtime.InMemoryReadModelStore<>();");
    [Fact] void should_qualify_the_colliding_guard_return_type() => _configurations["PersistentReadModelSafety"].ShouldContain("public static axonviews.runtime.persistent.PersistentReadModelSafety persistentReadModelSafety()");
    [Fact] void should_qualify_the_colliding_guard_constructor() => _configurations["PersistentReadModelSafety"].ShouldContain("return new axonviews.runtime.persistent.PersistentReadModelSafety(");
    [Fact] void should_keep_the_application_model_short_in_the_projector() => _projector.ShouldContain("private final axonviews.runtime.ReadModelStore<Object, ReadModelStore> store;");
    [Fact] void should_qualify_the_projector_constructor_parameter() => _projector.ShouldContain("axonviews.runtime.ReadModelStore<Object, ReadModelStore> store)");
    [Fact] void should_qualify_the_scoped_projector_store() => _scopedProjector.ShouldContain("private final axonviews.runtime.ReadModelStore<Object, ReadModelStore> store;");
    [Fact] void should_qualify_the_scoped_projector_constructor() => _scopedProjector.ShouldContain("public BoardProjectionProjector(axonviews.runtime.ReadModelStore<Object, ReadModelStore> store)");
    [Fact] void should_qualify_the_reducer_projector_store() => _reducerProjector.ShouldContain("private final axonviews.runtime.ReadModelStore<Object, ReadModelStore> store;");
    [Fact] void should_qualify_the_reducer_projector_constructor() => _reducerProjector.ShouldContain("axonviews.runtime.ReadModelStore<Object, ReadModelStore> store, ReadModelStoreReducer reducer)");
}
