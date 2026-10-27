// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_a_compliance_marked_enum_concept_is_declared : given.a_model_with_behaviors
{
    SemanticConcept concept;
    AxonBehaviorDecision[] decisions;

    void Establish() =>
        concept = new(SemanticId.Create(SemanticKind.Concept, "Diagnosis"), "Diagnosis", SemanticPrimitiveType.Text, ["Flu", "Cold"], []) { Compliance = new(new SemanticPersonalData(), null) };

    // The model validator refuses this shape today; the walk is the defense for a model that reaches Axon without it.
    void Because() => decisions = [.. AxonBehaviorAdmission.ConceptDecisions(request.Model.Application with { Concepts = request.Model.Application.Concepts.Add(concept) }, Java.JavaNames.PackageSegment(request.Model.Application.Name))];

    [Fact] void should_report_compliance_on_the_concept() => decisions.Single().Owner.ShouldEqual(concept.Id);
    [Fact] void should_use_the_scalar_compliance_code() => decisions.Single().Code.ShouldEqual("STAGE-AXON-COMPLIANCE");
    [Fact] void should_use_a_member_precise_model_path() => decisions.Single().ModelPath.ShouldEqual(concept.Id + "/compliance");
    [Fact] void should_place_the_comment_in_the_concepts_package() => decisions.Single().Package.EndsWith(".concepts", StringComparison.Ordinal).ShouldBeTrue();

    [Fact]
    void should_not_report_an_unmarked_enum_concept() =>
        AxonBehaviorAdmission.ConceptDecisions(request.Model.Application with { Concepts = [concept with { Compliance = null }] }, Java.JavaNames.PackageSegment(request.Model.Application.Name)).ShouldBeEmpty();
}
