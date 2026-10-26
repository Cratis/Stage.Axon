// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_ViewJavaTypes;

public class when_building_type_references : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ViewJavaTypes types;
    JavaCodeBuilder code;

    void Establish()
    {
        types = new(request.Model.Application);
        code = new JavaCodeBuilder("example.views").Declare("ExampleView");
    }

    [Fact]
    void should_import_uuid_collections()
    {
        var type = Primitive(SemanticPrimitiveType.Uuid) with { IsCollection = true };
        types.Type(type, code).ShouldEqual("List<UUID>");
        var source = code.Build();
        source.ShouldContain("import java.util.List;");
        source.ShouldContain("import java.util.UUID;");
    }

    [Fact]
    void should_keep_legacy_type_rendering_unchanged() => types.Type(Primitive(SemanticPrimitiveType.Uuid)).ShouldEqual("java.util.UUID");

    [Fact]
    void should_import_event_types()
    {
        var contract = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).First();
        types.EventType(contract.Id, code).ShouldEqual(contract.Name);
        code.Build().ShouldContain($"import {types.EventType(contract.Id)};");
    }

    [Fact]
    void should_import_date_parsers()
    {
        types.Parse(Primitive(SemanticPrimitiveType.Date), "text", code).ShouldEqual("LocalDate.parse(text)");
        code.Build().ShouldContain("import java.time.LocalDate;");
    }

    [Fact]
    void should_import_collection_literals()
    {
        types.Literal(new SemanticArrayValue([SemanticValue.Text("one")]), Primitive(SemanticPrimitiveType.Text) with { IsCollection = true }, code).ShouldEqual("List.of(\"one\")");
        code.Build().ShouldContain("import java.util.List;");
    }

    [Fact]
    void should_import_decimal_defaults()
    {
        types.Default(Primitive(SemanticPrimitiveType.DecimalNumber), code).ShouldEqual("BigDecimal.ZERO");
        code.Build().ShouldContain("import java.math.BigDecimal;");
    }

    [Fact]
    void should_keep_a_concept_named_big_decimal_short()
    {
        var concept = request.Model.Application.Concepts[0] with { Name = "BigDecimal", Primitive = SemanticPrimitiveType.DecimalNumber, Values = [], Numbers = null };
        var collision = new ViewJavaTypes(request.Model.Application with { Concepts = [concept] });
        var type = new SemanticTypeReference(SemanticTypeReferenceKind.Concept, SemanticPrimitiveType.DecimalNumber, concept.Id, false, false);
        collision.Parse(type, "text", code).ShouldEqual("new BigDecimal(new java.math.BigDecimal(text))");
        code.Build().ShouldContain($"import {types.BasePackage}.concepts.BigDecimal;");
        code.Build().ShouldNotContain("import java.math.BigDecimal;");
    }

    static SemanticTypeReference Primitive(SemanticPrimitiveType primitive) => new(SemanticTypeReferenceKind.Primitive, primitive, default, false, false);
}
