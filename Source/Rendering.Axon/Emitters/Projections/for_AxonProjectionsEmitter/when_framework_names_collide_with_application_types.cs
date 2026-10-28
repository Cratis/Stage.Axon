// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_framework_names_collide_with_application_types : given.a_view_request
{
    [Theory]
    [InlineData("Long")]
    [InlineData("Boolean")]
    [InlineData("Math")]
    [InlineData("Object")]
    void should_keep_flat_read_model_names_distinct_from_framework_types(string name)
    {
        var application = Rewrite(_request.Model.Application, model => model.Name == "Total" ? model with { Name = name } : model);
        var context = Emit(application);
        var repository = Artifact(context, name + "Repository");
        var entity = Artifact(context, name + "Entity");
        var projector = Artifact(context, "TotalProjectionProjector");
        repository.ShouldContain($"JpaRepository<{name}Entity, String>");
        entity.ShouldContain($"public {name} toModel()");
        projector.ShouldContain($"private final {name}Repository repository;");
        projector.ShouldContain($"repository.save(new {name}Entity(key, state));");
    }

    [Theory]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long")]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean")]
    void should_keep_uncaptured_parsers_short(SemanticPrimitiveType primitive, string name)
    {
        var types = new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name));
        var code = new Java.JavaCodeBuilder("example");
        types.Parse(new(SemanticTypeReferenceKind.Primitive, primitive, default, false, false), "value", code).ShouldEqual($"{name}.valueOf(value)");
    }

    AxonEmissionContext Emit(SemanticApplication application)
    {
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application);
        var context = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        return context;
    }

    static SemanticApplication Rewrite(SemanticApplication application, Func<SemanticReadModel, SemanticReadModel> model, Func<SemanticSlice, SemanticSlice>? transformSlice = null)
    {
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => (transformSlice?.Invoke(slice) ?? slice) with { ReadModels = [.. slice.ReadModels.Select(model)] })]
        };
        return application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
    }

    static string Artifact(AxonEmissionContext context, string name) => Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)));
}
