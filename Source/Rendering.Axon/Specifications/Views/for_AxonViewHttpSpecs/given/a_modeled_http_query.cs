// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewHttpSpecs.given;

public class a_modeled_http_query : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    protected string Render(Func<SemanticSlice, SemanticSlice> transform)
    {
        var application = _request.Model.Application;
        application = application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(feature => Transform(feature, transform))] })]
        };
        var request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        var located = ScopeSelection.Select(request).Single(owner => owner.Slice.Specifications.Any(spec => spec.Name == "IncreasingTheTotal"));
        var specification = located.Slice.Specifications.Single(spec => spec.Name == "IncreasingTheTotal");
        var model = located.Slice.ReadModels.Single();
        AxonSpecificationsEmitter.Register(context, specification.Id, "example.ViewTest", "increasingTheTotal");
        new AxonViewHttpSpecs(context, new ViewJavaTypes(application, Java.JavaNames.PackageSegment(application.Name)), located, model).Register(located.Slice.Projections.Single(), specification);
        AxonHttpTests.Emit(context);
        return Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledHttpRoundTripTest.java", StringComparison.Ordinal)));
    }

    static SemanticFeature Transform(SemanticFeature feature, Func<SemanticSlice, SemanticSlice> transform) => feature with
    {
        Features = [.. feature.Features.Select(child => Transform(child, transform))],
        Slices = [.. feature.Slices.Select(transform)]
    };
}
