// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_scope_property_types_collide_with_java_names : given.a_view_request
{
    [Theory]
    [InlineData("type", "BoardDetails")]
    [InlineData("type", "Card")]
    [InlineData("concept", "BoardId")]
    void should_keep_application_string_names_and_qualify_all_framework_conversions(string kind, string original)
    {
        var code = Render(kind, original, "String");
        code.ShouldContain($"import axonviews.{(kind == "type" ? "types" : "concepts")}.String;");
        code.ShouldNotContain("import java.lang.String;");
        code.ShouldContain(".put(java.lang.String.valueOf(java.lang.String.valueOf(event.cardId().value())), initial ->");
        code.ShouldContain(".get(java.lang.String.valueOf(state.cardId().value()))");
        code.ShouldContain("var identity = java.lang.String.valueOf(item.cardId().value());");
        code.ShouldNotContain(".put(String.valueOf(");
        code.ShouldNotContain(".get(String.valueOf(");
        code.ShouldNotContain("var identity = String.valueOf(");
        if (original == "Card") code.ShouldContain("private Change<String> applyRootChild0");
        if (original == "BoardDetails") code.ShouldContain("new String(");
    }

    [Fact]
    void should_resolve_decimal_defaults_after_claiming_a_big_decimal_concept()
    {
        var code = Render("concept", "BoardId", "BigDecimal", decimalCount: true);
        code.ShouldContain("import axonviews.concepts.BigDecimal;");
        code.ShouldNotContain("import java.math.BigDecimal;");
        code.ShouldContain("new BoardDetails(null, java.math.BigDecimal.ZERO)");
        code.ShouldContain("java.math.BigDecimal.ONE");
    }

    [Fact]
    void should_qualify_java_lang_object_after_claiming_an_application_type()
    {
        var code = Render("type", "BoardDetails", "Object");
        code.ShouldContain("import axonviews.types.Object;");
        code.ShouldContain("java.lang.Object key, java.lang.Object parentKey");
        code.ShouldContain("private Change<Object> applyRootNested0");
    }

    [Fact]
    void should_keep_the_local_change_helper_distinct_from_an_application_record()
    {
        var code = Render("type", "BoardDetails", "Change");
        code.ShouldNotContain("import axonviews.types.Change;");
        code.ShouldContain("private record Change<T>(T value, boolean touched, boolean ancestors)");
        code.ShouldContain("private Change<axonviews.types.Change> applyRootNested0");
        code.ShouldContain("new axonviews.types.Change(");
    }

    [Fact]
    void should_keep_noncolliding_framework_names_short()
    {
        var code = Render("type", "BoardDetails", "Details", decimalCount: true);
        code.ShouldContain("import axonviews.types.Details;");
        code.ShouldContain("import java.math.BigDecimal;");
        code.ShouldContain("new Details(null, BigDecimal.ZERO)");
        code.ShouldContain("BigDecimal.ONE");
        code.ShouldContain(".get(String.valueOf(state.cardId().value()))");
        code.ShouldContain("var identity = String.valueOf(item.cardId().value());");
    }

    string Render(string kind, string original, string name, bool decimalCount = false)
    {
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Concepts = [.. application.Concepts.Select(concept => kind == "concept" && concept.Name == original ? concept with { Name = name } : concept)],
            Types = [.. application.Types.Select(type => type with
            {
                Name = kind == "type" && type.Name == original ? name : type.Name,
                Properties = [.. type.Properties.Select(property => decimalCount && type.Name == "BoardDetails" && property.Name == "count"
                    ? property with { Type = property.Type with { Primitive = SemanticPrimitiveType.DecimalNumber } } : property)]
            })]
        });
        var context = new AxonEmissionContext(_request with { Model = model }, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        return Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/BoardProjectionProjector.java", StringComparison.Ordinal)));
    }
}
