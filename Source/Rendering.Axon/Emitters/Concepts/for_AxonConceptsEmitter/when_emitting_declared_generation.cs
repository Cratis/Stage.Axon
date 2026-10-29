// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts.for_AxonConceptsEmitter;

public class when_emitting_declared_generation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Establish() => context = new(request, new AxonDegradation(request.Profile));

    [Fact]
    void should_emit_the_uuid_factory_using_fundamentals()
    {
        var template = loaded.Model.Application.Concepts.First(concept => concept.Primitive == SemanticPrimitiveType.Uuid);
        var concept = new SemanticConcept(template.Id, "GeneratedId", SemanticPrimitiveType.Uuid, [], []) { Generation = new("uuid") };
        var code = AxonConceptsEmitter.Scalar(context, concept);
        code.ShouldContain("public static GeneratedId create() { return new GeneratedId(GenerateValue.uuid()); }");
        code.ShouldContain("Generatable<GeneratedId>");
        code.ShouldContain("import io.cratis.fundamentals.concepts.GenerateValue;");
        code.ShouldContain("import io.cratis.fundamentals.concepts.Generatable;");
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
        context.Degradation.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, ArtifactRenderDiagnosticSeverity.Warning)]
    [InlineData(true, ArtifactRenderDiagnosticSeverity.Error)]
    public void should_report_unsupported_options_in_the_summary_and_code_and_refuse_in_strict_mode(bool strict, ArtifactRenderDiagnosticSeverity severity)
    {
        var profile = AxonRendering.CreateProfile(strict: strict);
        context = new(request with { Profile = profile }, new AxonDegradation(profile));
        var template = loaded.Model.Application.Concepts.First(concept => concept.Primitive == SemanticPrimitiveType.Uuid);
        var concept = new SemanticConcept(template.Id, "GeneratedId", SemanticPrimitiveType.Uuid, [], []) { Generation = new("uuid") { Pattern = "ignored" } };
        var code = AxonConceptsEmitter.Scalar(context, concept);
        code.ShouldContain("public static GeneratedId create() { return new GeneratedId(GenerateValue.uuid()); }");
        code.ShouldContain($"// Screenplay wanted to generate GeneratedId values using declared UUID options ({concept.Id}/generation), but this is not supported by the Axon target yet (STAGE-AXON-GENERATION-OPTIONS).");
        code.ShouldNotContain("throw new");
        var diagnostic = Assert.Single(context.Degradation.UnsupportedConstructs);
        diagnostic.Code.ShouldEqual("STAGE-AXON-GENERATION-OPTIONS");
        diagnostic.Severity.ShouldEqual(severity);
        diagnostic.Artifact.ShouldEqual(concept.Id);
        UnsupportedSummary.Emit(context);
        var summary = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan());
        Assert.Matches($@"\| STAGE-AXON-GENERATION-OPTIONS \| [^\n]*{template.Name} concept \(generation\) \| [^\n]*\| generate GeneratedId values using declared UUID options \| Generation options are not represented by UUID value creation\. \|", summary);
        if (strict) Assert.Single(context.Degradation.Refusals).Codes.ShouldEqual("STAGE-AXON-GENERATION-OPTIONS");
        else context.Degradation.Refusals.ShouldBeEmpty();
    }

    [Fact]
    void should_preserve_explicit_numeric_enum_values()
    {
        var template = loaded.Model.Application.Concepts[0];
        var concept = new SemanticConcept(template.Id, "Priority", SemanticPrimitiveType.Unknown, ["low", "high"], []) { Numbers = [10, 30] };
        var code = ConceptEnums.Emit(context, concept);
        code.ShouldContain("LOW(10)");
        code.ShouldContain("HIGH(30)");
        code.ShouldContain("@JsonValue\n    public Integer value()");
        code.ShouldContain("@JsonCreator\n    public static Priority fromValue(Integer value)");
        code.ShouldContain("candidate.value.equals(value)");
        code.ShouldContain("throw new IllegalArgumentException(\"Unknown Priority value: \" + value)");
    }
}
