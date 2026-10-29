// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_a_native_reducer_model_is_named_object : a_view_request
{
    string _java;
    string _projector;
    string _model;

    void Because()
    {
        var located = ScopeSelection.Select(_request).Single(slice => !slice.Slice.Reducers.IsEmpty);
        var model = located.Slice.ReadModels.Single() with { Name = "Object" };
        var spec = located.Slice.Specifications.Single(spec => spec.Name == "DeleteAfterInsert");
        spec = spec with { GivenEvents = [spec.GivenEvents[^1]] };
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice.Id == located.Slice.Id ? slice with { ReadModels = [model], Specifications = [spec] } : slice)]
        };
        var application = _request.Model.Application;
        var esm = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        var unused = located.Slice.Reducers.Single().Transitions.Single(transition => transition.EventContract != spec.GivenEvents[0].EventContract);
        var request = _request with { Model = esm, ImplementationContents = _request.ImplementationContents.Remove(unused.RequirementId) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        _java = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ObjectViewSpecificationsTest.java", StringComparison.Ordinal)));
        _projector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ObjectReducer.java", StringComparison.Ordinal)));
        _model = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Object.java", StringComparison.Ordinal)));
    }

    [Fact] void should_emit_an_executable_native_scenario() => _java.ShouldNotContain("@Disabled");
    [Fact] void should_keep_the_application_record_as_the_store_value() => _model.ShouldContain("public record Object(CounterId id, Long amount)");
    [Fact] void should_type_the_repository_of_the_application_record_in_the_native_specification() => _java.ShouldContain("ObjectRepository repository;");
    [Fact] void should_inject_the_repository_in_the_projector() => _projector.ShouldContain("public ObjectReducer(ObjectRepository repository)");
    [Fact] void should_not_reference_the_removed_runtime_store() => _java.ShouldNotContain("ReadModelStore");
}
