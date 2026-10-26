// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts.for_AxonConceptsEmitter;

public class when_emitting_record_types : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    SemanticProperty property;

    void Establish()
    {
        context = new(request, new(request.Profile));
        property = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0].Properties[0];
    }

    [Fact]
    void should_import_collection_elements_and_the_collection()
    {
        var field = property with { Name = "identities", Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Uuid) with { IsCollection = true } };
        var code = Record("Envelope", [field]);
        code.ShouldContain("public record Envelope(List<UUID> identities)");
        code.ShouldContain("import java.util.List;");
        code.ShouldContain("import java.util.UUID;");
    }

    [Fact]
    void should_keep_a_record_named_list_and_qualify_the_framework_collection()
    {
        var field = property with { Name = "dates", Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Date) with { IsCollection = true } };
        var code = Record("List", [field]);
        code.ShouldContain("public record List(java.util.List<LocalDate> dates)");
        code.ShouldContain("import java.time.LocalDate;");
        code.ShouldNotContain("import java.util.List;");
    }

    [Fact]
    void should_import_json_annotations_only_for_renamed_components()
    {
        var code = Record("Envelope", [property with { Name = "class", Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime) }]);
        code.ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
        code.ShouldContain("@JsonProperty(\"class\") OffsetDateTime class_");
        code.ShouldContain("import java.time.OffsetDateTime;");
        Record("Envelope", [property with { Name = "validName" }]).ShouldNotContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    }

    [Fact]
    void should_claim_application_elements_before_framework_types_even_when_the_property_is_later()
    {
        var concept = loaded.Model.Application.Concepts[0] with { Name = "BigDecimal" };
        var application = loaded.Model.Application with { Concepts = [concept, .. loaded.Model.Application.Concepts.Skip(1)] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, application);
        context = new(request with { Model = model }, new(request.Profile));
        var decimalField = property with { Name = "amount", Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DecimalNumber) };
        var conceptField = property with { Name = "values", Type = SemanticTypeReference.ForConcept(concept.Id) with { IsCollection = true } };
        var code = Record("Envelope", [decimalField, conceptField]);
        code.ShouldContain("import " + context.BasePackage + ".concepts.BigDecimal;");
        code.ShouldContain("public record Envelope(java.math.BigDecimal amount, List<BigDecimal> values)");
        code.ShouldNotContain("import java.math.BigDecimal;");
    }

    [Fact]
    void should_keep_the_legacy_qualified_overload_for_non_owned_callers()
    {
        var type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Uuid) with { IsCollection = true };
        JavaSemanticTypes.TypeFor(context, type).ShouldEqual("java.util.List<java.util.UUID>");
        var builder = new JavaCodeBuilder(context.BasePackage).Declare("Envelope");
        JavaSemanticTypes.TypeFor(context, type, builder).ShouldEqual("List<UUID>");
    }

    string Record(string name, IEnumerable<SemanticProperty> fields) => AxonConceptsEmitter.Record(context, context.BasePackage + ".types", name, fields);
}
