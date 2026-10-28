// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts.for_AxonConceptsEmitter;

public class when_a_validation_has_no_java_implementation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    const string Reason = "The validation rule has no Java implementation; the generated validator accepts every value.";
    AxonEmissionContext context;
    string code;
    string summary;

    void Because()
    {
        context = new(request, new(request.Profile));
        var seed = request.Model.Application.Concepts.First(concept => concept.Primitive == SemanticPrimitiveType.DecimalNumber);
        var concept = seed with { Validations = [new(default, SemanticValidationRuleKind.DecimalPlaces, new SemanticNumberValue(2), "Two decimal places required")] };
        code = AxonConceptsEmitter.Scalar(context, concept);
        UnsupportedSummary.Emit(context);
        summary = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan());
    }

    [Fact] void should_not_throw_for_the_unimplemented_rule() => code.ShouldNotContain("throw new");
    [Fact] void should_explain_the_difference() => code.ShouldContain("// Differs from Screenplay: " + Reason);
    [Fact] void should_warn_with_the_validation_code() => context.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual("STAGE-AXON-IMPL-VALIDATION");
    [Fact] void should_emit_a_warning() => context.Degradation.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_explain_the_warning() => context.Degradation.UnsupportedConstructs.Single().Message.ShouldEqual(Reason);
    [Fact] void should_document_the_no_op() => summary.ShouldContain("| STAGE-AXON-IMPL-VALIDATION | ");
    [Fact] void should_document_the_reason() => summary.ShouldContain("| " + Reason + " |");
}
