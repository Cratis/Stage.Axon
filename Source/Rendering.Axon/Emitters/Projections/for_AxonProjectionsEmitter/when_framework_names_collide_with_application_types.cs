// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_framework_names_collide_with_application_types : given.a_view_request
{
    [Theory]
    [InlineData("Long", SemanticPrimitiveType.WholeNumber, "identity")]
    [InlineData("Long", SemanticPrimitiveType.WholeNumber, "context")]
    [InlineData("Long", SemanticPrimitiveType.WholeNumber, "literal")]
    [InlineData("Boolean", SemanticPrimitiveType.Boolean, "identity")]
    [InlineData("Boolean", SemanticPrimitiveType.Boolean, "context")]
    [InlineData("Boolean", SemanticPrimitiveType.Boolean, "literal")]
    void should_qualify_each_scope_mapping_conversion(string name, SemanticPrimitiveType primitive, string path)
    {
        var application = _request.Model.Application;
        var source = Sdk.ScopeSelection.Locate(application).SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "Board").Properties.Single(property => property.Name == "source");
        SemanticValue literal = primitive == SemanticPrimitiveType.Boolean ? new SemanticBooleanValue(true) : new SemanticNumberValue(42);
        SemanticProjectionValue value = path switch
        {
            "identity" => new SemanticProjectionEventSourceIdentity(),
            "context" => new SemanticProjectionEventContextValue("eventSourceId"),
            _ => new SemanticProjectionLiteral(literal)
        };
        var model = Rewrite(application with { Types = [.. application.Types.Select(type => type.Name == "BoardDetails" ? type with { Name = name } : type)] },
            model => model.Name == "Board" ? model with { Properties = [.. model.Properties.Select(property => property.Id == source.Id ? property with { Type = new(SemanticTypeReferenceKind.Primitive, primitive, default, false, false) } : property)] } : model,
            slice => slice with
            {
                Projections = [.. slice.Projections.Select(projection => projection.Name == "BoardProjection" ? projection with { Scope = projection.Scope! with { From = [.. projection.Scope.From.Select(transition => transition with { Mappings = [.. transition.Mappings.Select(mapping => mapping.Target.Contains(source.Id) ? mapping with { Source = value } : mapping)] })] } } : projection)],
                Specifications = [.. slice.Specifications.Select(specification => specification with { ThenReadModels = [.. specification.ThenReadModels.Select(expected => expected with { Values = [.. expected.Values.Select(value => value.TargetProperty == source.Id ? value with { Value = literal } : value)] })] })]
            });
        var code = Artifact(Emit(model), "BoardProjectionProjector");
        code.ShouldContain($"import axonviews.types.{name};");
        if (path == "literal")
        {
            code.ShouldContain($"state.withSource({(primitive == SemanticPrimitiveType.Boolean ? "true" : "42L")})");
            code.Replace($"java.lang.{name}.", string.Empty, StringComparison.Ordinal).ShouldNotContain($"{name}.");
        }
        else
        {
            code.ShouldContain($"state.withSource(java.lang.{name}.valueOf(");
        }
        code.ShouldNotContain($"state.withSource({name}.valueOf(");
        code.ShouldNotContain($"import java.lang.{name};");
    }

    [Fact]
    void should_qualify_numbered_enum_wrapping()
    {
        var application = _request.Model.Application;
        var concept = new SemanticConcept(SemanticId.Create(SemanticKind.Concept, "Math"), "Math", SemanticPrimitiveType.Text, ["b1", "b2", "b3", "b4", "b5", "b6"], []) { Numbers = [1, 2, 3, 4, 5, 6] };
        var model = Rewrite(application with { Concepts = [.. application.Concepts, concept] },
            model => model.Name == "Board" ? model with { Properties = [.. model.Properties.Select(property => property.Name == "source" ? property with { Type = new(SemanticTypeReferenceKind.Concept, SemanticPrimitiveType.Unknown, concept.Id, false, false) } : property)] } : model);
        var code = Artifact(Emit(model), "BoardProjectionProjector");
        code.ShouldContain("import axonviews.concepts.Math;");
        code.ShouldContain("Math.fromValue(java.lang.Math.toIntExact(Long.valueOf(String.valueOf(event.boardId().value()))))");
        code.ShouldNotContain("Math.fromValue(Math.toIntExact(");
    }

    [Theory]
    [InlineData("Long")]
    [InlineData("Boolean")]
    [InlineData("Math")]
    [InlineData("Object")]
    void should_keep_flat_read_model_names_distinct_from_framework_types(string name)
    {
        var application = Rewrite(_request.Model.Application, model => model.Name == "Total" ? model with { Name = name } : model);
        var context = Emit(application);
        var configuration = Artifact(context, name + "StoreConfiguration");
        var projector = Artifact(context, "TotalProjectionProjector");
        var objectType = name == "Object" ? "java.lang.Object" : "Object";
        configuration.ShouldContain($"public ReadModelStore<{objectType}, {name}> persistentStore(");
        projector.ShouldContain("private void apply(Increased event");
        projector.ShouldContain($"ReadModelStore<{objectType}, {name}> store)");
        if (name == "Object")
        {
            configuration.ShouldNotContain("ReadModelStore<Object, Object>");
            projector.ShouldNotContain("ReadModelStore<Object, Object>");
        }
    }

    [Fact]
    void should_import_a_scope_property_named_object()
    {
        var application = _request.Model.Application;
        var code = Artifact(Emit(application with { Types = [.. application.Types.Select(type => type.Name == "BoardDetails" ? type with { Name = "Object" } : type)] }), "BoardProjectionProjector");
        code.ShouldContain("import axonviews.types.Object;");
        code.ShouldContain("java.lang.Object key, java.lang.Object parentKey");
        code.ShouldNotContain("EventMessage message, Object key");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_not_shadow_a_root_read_model_named_change(bool usesHelperName)
    {
        var name = usesHelperName ? "ScopeChange" : "Change";
        var helper = usesHelperName ? "ScopeChangeResult" : "ScopeChange";
        var code = Artifact(Emit(Rewrite(_request.Model.Application, model => model.Name == "Board" ? model with { Name = name } : model)), "BoardProjectionProjector");
        code.ShouldContain($"private record {helper}<T>");
        code.ShouldContain($"private {helper}<{name}> applyRootBoardCreated");
        code.ShouldContain($"private final ReadModelStore<Object, {name}> store;");
        code.ShouldNotContain($"private record {name}<T>");
    }

    [Fact]
    void should_keep_uncaptured_framework_names_short()
    {
        var code = Artifact(Emit(_request.Model.Application), "BoardProjectionProjector");
        code.ShouldContain("Object key, Object parentKey");
        code.ShouldContain("String.valueOf(event.boardId().value())");
        code.ShouldNotContain("java.lang.");
    }

    [Theory]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long")]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean")]
    void should_keep_uncaptured_parsers_short(SemanticPrimitiveType primitive, string name)
    {
        var types = new ViewJavaTypes(_request.Model.Application);
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
