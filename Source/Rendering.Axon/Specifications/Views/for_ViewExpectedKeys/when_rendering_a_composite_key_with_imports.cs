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
        var identifier = original.Properties.Single(property => property.IsIdentifier) with { IsIdentifier = false };
        var label = original.Properties.Single(property => property.Name == "name");
        var model = original with { Properties = [identifier, label] };
        var key = new SemanticCompositeValue([
            new(identifier.Id, new SemanticTextValue("6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021")),
            new(label.Id, new SemanticTextValue("Fittings"))]);
        var code = new JavaCodeBuilder("example.specs");
        _expression = ViewExpectedKeys.Expression(key, model, new ViewJavaTypes(_request.Model.Application), code);
        _java = code.Line(_expression).Build();
    }

    [Fact] void should_use_short_collection_names() => _expression.ShouldContain("Map.ofEntries(Map.entry(");
    [Fact] void should_use_short_concept_and_uuid_names() => _expression.ShouldContain("new CategoryId(UUID.fromString(");
    [Fact] void should_import_the_collection() => _java.ShouldContain("import java.util.Map;");
    [Fact] void should_import_the_uuid() => _java.ShouldContain("import java.util.UUID;");
    [Fact] void should_import_the_concept() => _java.ShouldContain("import nativeviews.concepts.CategoryId;");
}
