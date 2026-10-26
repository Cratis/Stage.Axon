// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_auditing_native_implementation_support : Specification
{
    [Theory]
    [InlineData("SemanticCommand.Handler")]
    [InlineData("SemanticCommandHandler.RequirementId")]
    [InlineData("SemanticReactionTrigger.RequirementId")]
    [InlineData("SemanticImplementationRole.QueryPerformer")]
    [InlineData("SemanticImplementationRequirement.ContentHash")]
    public void should_describe_conditional_native_support_without_claiming_universal_rendering(string member)
    {
        var entry = SemanticSurfaceLedger.Entries[member];
        entry.Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        entry.Detail.ShouldContain("LF-only");
        entry.Detail.ShouldContain("SHA-256");
        entry.Detail.ShouldContain("STAGE-AXON-IMPL");
        Assert.DoesNotContain("STAGE-AXON-0403", entry.Detail);
    }

    [Theory]
    [InlineData("SemanticCommand.Reads", "STAGE-AXON-0106", "single-level")]
    [InlineData("SemanticDecisionRead.Key", "STAGE-AXON-0106", "source input")]
    [InlineData("SemanticCommand.Produces", "STAGE-AXON-0110", "native")]
    [InlineData("SemanticSlice.Constraints", "STAGE-AXON-0801", "UniquePropertyValue")]
    [InlineData("SemanticCommand.Requirements", "STAGE-AXON-0107", "validation.reject")]
    [InlineData("SemanticProducedEvent.Tags", "STAGE-AXON-APPEND", "declarative")]
    [InlineData("SemanticCommand.Route", "STAGE-AXON-ROUTING", "primary")]
    [InlineData("SemanticConcept.Generation", "STAGE-AXON-GENERATION", "UUID")]
    [InlineData("SemanticConcept.Compliance", "STAGE-AXON-COMPLIANCE", "enum")]
    [InlineData("SemanticEventContract.PriorRevisions", "STAGE-AXON-EVOLUTION", "current")]
    [InlineData("SemanticProperty.IsIdentifier", "STAGE-AXON-EVENT-TAGS", "primary")]
    [InlineData("SemanticProperty.Type", "STAGE-AXON-TYPE", "Object")]
    [InlineData("SemanticCommand.Authorization", "STAGE-AXON-AUTHORIZATION", "denyAll")]
    [InlineData("SemanticReaction.Triggers", "STAGE-AXON-0504", "registration")]
    [InlineData("SemanticReaction.RunsAs", "STAGE-AXON-0505", "System")]
    [InlineData("SemanticReactionTriggerKind.Schedule", "STAGE-AXON-0507", "process-local")]
    [InlineData("SemanticProjectionFrom.Key", "STAGE-AXON-0305", "flat")]
    [InlineData("SemanticProjectionMapping.Source", "STAGE-AXON-0307", "skip")]
    [InlineData("SemanticQueryFilterValue.Filter", "STAGE-AXON-0501", "disabled")]
    [InlineData("SemanticSpecification.ThenQueries", "STAGE-AXON-0112", "command")]
    [InlineData("SemanticSpecification.ThenQueries", "STAGE-AXON-0501", "view")]
    [InlineData("SemanticSpecification.ThenQueries", "STAGE-AXON-0504", "reaction")]
    [InlineData("SemanticPerformedQueryShape.Arguments", "STAGE-AXON-0401", "performed")]
    [InlineData("SemanticSlice.Specifications", "STAGE-AXON-SPEC", "unclaimed")]
    public void should_describe_conditional_support_and_distinguish_rendering_modes(string member, string code, string condition)
    {
        var entry = SemanticSurfaceLedger.Entries[member];
        entry.Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        entry.Detail.ShouldContain(code);
        entry.Detail.ShouldContain(condition);
        entry.Detail.ShouldContain("default rendering warns");
        entry.Detail.ShouldContain("strict rendering errors and refuses the plan");
    }

    [Theory]
    [InlineData("SemanticReducerTransition.RequirementId")]
    [InlineData("SemanticImplementationRole.ReducerTransition")]
    public void should_retain_truthful_reducer_degradation(string member)
    {
        var entry = SemanticSurfaceLedger.Entries[member];
        entry.Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        entry.Detail.ShouldContain("STAGE-AXON-0306");
        entry.Detail.ShouldContain("disabled");
    }
}
