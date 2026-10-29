// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_ViewExpectedKeys;

public class when_rendering_a_composite_key_with_imports : for_NativeViewSpecifications.given.a_native_view_request
{
    string _expression;
    string _java;

    void Because()
    {
        var original = _located.Slice.ReadModels.Single();
        var identifier = original.Properties.Single(property => property.IsIdentifier);
        var label = original.Properties.Single(property => property.Name == "name") with { IsIdentifier = true };
        var model = original with { Properties = [identifier, label] };
        var key = new SemanticCompositeValue([
            new(identifier.Id, new SemanticTextValue("6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021")),
            new(label.Id, new SemanticTextValue("Fittings"))]);
        var code = new JavaCodeBuilder("example.specs");
        _expression = ViewExpectedKeys.Expression(key, model, new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name)), code);
        _java = code.Line(_expression).Build();
    }

    [Fact] void should_construct_the_typed_key() => _expression.ShouldContain("new CategoriesKey(");
    [Fact] void should_unwrap_the_concept_for_the_key() => _expression.ShouldContain("(new CategoryId(UUID.fromString(");
    [Fact] void should_use_the_underlying_column_type() => _expression.ShouldContain(")).value(), \"Fittings\"");
    [Fact] void should_import_the_uuid() => _java.ShouldContain("import java.util.UUID;");
    [Fact] void should_import_the_concept() => _java.ShouldContain("import nativeviews.concepts.CategoryId;");
}
