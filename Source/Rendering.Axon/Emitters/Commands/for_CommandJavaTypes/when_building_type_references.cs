// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandJavaTypes;

public class when_building_type_references : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    CommandJavaTypes types;
    JavaCodeBuilder code;

    void Establish()
    {
        types = new(request.Model.Application);
        code = new JavaCodeBuilder("example.commands").Declare("ExampleCommand");
    }

    [Fact]
    void should_import_uuid_collections()
    {
        types.Type(Primitive(SemanticPrimitiveType.Uuid) with { IsCollection = true }, code).ShouldEqual("List<UUID>");
        var source = code.Build();
        source.ShouldContain("import java.util.List;");
        source.ShouldContain("import java.util.UUID;");
    }

    [Fact] void should_preserve_legacy_type_output() => types.Type(Primitive(SemanticPrimitiveType.Uuid)).ShouldEqual("java.util.UUID");

    [Fact]
    void should_not_import_java_lang_types()
    {
        types.Type(Primitive(SemanticPrimitiveType.Text), code).ShouldEqual("String");
        code.Build().ShouldNotContain("import java.lang.String;");
    }

    [Fact]
    void should_claim_application_names_before_framework_types()
    {
        var concept = request.Model.Application.Concepts[0] with { Name = "UUID" };
        var collision = new CommandJavaTypes(request.Model.Application with { Concepts = [concept] });
        var conceptType = new SemanticTypeReference(SemanticTypeReferenceKind.Concept, concept.Primitive, concept.Id, false, false);
        collision.Claim(conceptType, code);
        collision.Type(Primitive(SemanticPrimitiveType.Uuid), code).ShouldEqual("java.util.UUID");
        collision.Type(conceptType, code).ShouldEqual("UUID");
        var source = code.Build();
        source.ShouldContain($"import {types.BasePackage}.concepts.UUID;");
        source.ShouldNotContain("import java.util.UUID;");
    }

    [Fact]
    void should_import_uuid_literal_factories()
    {
        types.Literal(SemanticValue.Text(Guid.Empty.ToString()), Primitive(SemanticPrimitiveType.Uuid), code).ShouldEqual($"UUID.fromString({JavaCodeBuilder.StringLiteral(Guid.Empty.ToString())})");
        code.Build().ShouldContain("import java.util.UUID;");
    }

    [Fact]
    void should_keep_collection_literal_semantics()
    {
        var value = new SemanticArrayValue([new SemanticNullValue(), SemanticValue.Text("one")]);
        types.Literal(value, Primitive(SemanticPrimitiveType.Text) with { IsCollection = true }, code).ShouldEqual("Arrays.asList(null, \"one\")");
        code.Build().ShouldContain("import java.util.Arrays;");
    }

    [Fact]
    void should_not_import_types_for_rejected_literals()
    {
        types.Literal(new SemanticNumberValue(1.5m), Primitive(SemanticPrimitiveType.WholeNumber), code).ShouldBeNull();
        code.Build().ShouldNotContain("import ");
    }

    [Fact]
    void should_keep_legacy_literal_output()
    {
        types.Literal(new SemanticNumberValue(1m), Primitive(SemanticPrimitiveType.DecimalNumber)).ShouldEqual("new java.math.BigDecimal(\"1\")");
    }

    [Fact]
    void should_keep_a_decimal_concept_short_when_its_framework_type_collides()
    {
        var concept = request.Model.Application.Concepts[0] with { Name = "BigDecimal", Primitive = SemanticPrimitiveType.DecimalNumber, Values = [], Numbers = null };
        var collision = new CommandJavaTypes(request.Model.Application with { Concepts = [concept] });
        var type = new SemanticTypeReference(SemanticTypeReferenceKind.Concept, concept.Primitive, concept.Id, false, false);
        collision.Literal(new SemanticNumberValue(1m), type, code).ShouldEqual("new BigDecimal(new java.math.BigDecimal(\"1\"))");
        code.Build().ShouldContain($"import {types.BasePackage}.concepts.BigDecimal;");
        code.Build().ShouldNotContain("import java.math.BigDecimal;");
    }

    static SemanticTypeReference Primitive(SemanticPrimitiveType primitive) => new(SemanticTypeReferenceKind.Primitive, primitive, default, false, false);
}
