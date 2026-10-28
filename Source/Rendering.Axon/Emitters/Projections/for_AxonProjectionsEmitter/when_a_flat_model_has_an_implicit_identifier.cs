// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_a_flat_model_has_an_implicit_identifier : given.a_view_request
{
    string _projector;

    void Because()
    {
        var application = _request.Model.Application;
        static SemanticReadModel Identify(SemanticReadModel model) => model.Name == "Total"
            ? model with { Properties = [.. model.Properties.Select(property => property.Name == "name" ? property with { IsIdentifier = true } : property)] }
            : model;
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { ReadModels = [.. slice.ReadModels.Select(Identify)] })]
        };
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        });
        var request = _request with { Model = model };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        _projector = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("TotalProjectionProjector.java", StringComparison.Ordinal)));
    }

    [Fact] void should_initialize_the_identifier_from_the_key_before_the_mappings() => _projector.ShouldContain("state = new Total(\"global\", state.amount(), state.count(), state.note());");
}
