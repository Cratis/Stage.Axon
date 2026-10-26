// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts.for_AxonConceptsEmitter;

public class when_resolving_type_collisions : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Establish() => context = new(request, new(request.Profile));

    [Fact]
    void should_keep_a_concept_named_concept_as_short()
    {
        var code = Scalar("ConceptAs", SemanticPrimitiveType.Text);
        code.ShouldContain("public record ConceptAs(String value) implements io.cratis.fundamentals.concepts.ConceptAs<String>");
        code.ShouldNotContain("import io.cratis.fundamentals.concepts.ConceptAs;");
    }

    [Fact]
    void should_qualify_uuid_when_the_concept_claims_its_name()
    {
        var code = Scalar("UUID", SemanticPrimitiveType.Uuid);
        code.ShouldContain("public record UUID(java.util.UUID value) implements ConceptAs<java.util.UUID>");
        code.ShouldNotContain("import java.util.UUID;");
    }

    [Fact]
    void should_qualify_decimal_when_the_concept_claims_its_name()
    {
        var code = Scalar("BigDecimal", SemanticPrimitiveType.DecimalNumber);
        code.ShouldContain("public record BigDecimal(java.math.BigDecimal value) implements ConceptAs<java.math.BigDecimal>");
        code.ShouldNotContain("import java.math.BigDecimal;");
    }

    [Fact]
    void should_keep_java_lang_distinct_from_a_modeled_string()
    {
        var code = Scalar("String", SemanticPrimitiveType.Text);
        code.ShouldContain("public record String(java.lang.String value)");
        code.ShouldContain("public java.lang.String toString()");
        code.ShouldNotContain("import java.lang.String;");
    }

    [Fact]
    void should_qualify_the_generator_when_the_concept_claims_its_name()
    {
        var concept = new SemanticConcept(loaded.Model.Application.Concepts[0].Id, "GenerateValue", SemanticPrimitiveType.Uuid, [], []) { Generation = new("uuid") };
        var code = AxonConceptsEmitter.Scalar(context, concept);
        code.ShouldContain("return new GenerateValue(io.cratis.fundamentals.concepts.GenerateValue.uuid());");
        code.ShouldNotContain("import io.cratis.fundamentals.concepts.GenerateValue;");
    }

    [Fact]
    void should_qualify_enum_annotations_when_the_enum_claims_their_name()
    {
        var concept = new SemanticConcept(loaded.Model.Application.Concepts[0].Id, "JsonValue", SemanticPrimitiveType.Text, ["one"], []);
        var code = ConceptEnums.Emit(context, concept);
        code.ShouldContain("public enum JsonValue");
        code.ShouldContain("@com.fasterxml.jackson.annotation.JsonValue");
        code.ShouldNotContain("import com.fasterxml.jackson.annotation.JsonValue;");
    }

    [Fact]
    void should_reserve_other_concepts_in_the_same_package_before_framework_names()
    {
        var concept = loaded.Model.Application.Concepts[0] with { Name = "Objects" };
        var application = loaded.Model.Application with { Concepts = [concept, .. loaded.Model.Application.Concepts.Skip(1)] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, application);
        var collisionContext = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        var scalar = new SemanticConcept(concept.Id, "Other", SemanticPrimitiveType.Text, [], []);
        var code = AxonConceptsEmitter.Scalar(collisionContext, scalar);
        code.ShouldContain("java.util.Objects.requireNonNull(value, \"value\");");
        code.ShouldNotContain("import java.util.Objects;");
    }

    string Scalar(string name, SemanticPrimitiveType primitive) => AxonConceptsEmitter.Scalar(context,
        new SemanticConcept(loaded.Model.Application.Concepts[0].Id, name, primitive, [], []));
}
