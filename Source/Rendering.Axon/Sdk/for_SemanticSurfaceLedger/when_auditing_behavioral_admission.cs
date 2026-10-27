// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_auditing_behavioral_admission : Specification
{
    [Fact]
    void should_not_retain_the_unclassified_sentinel() =>
        SemanticSurfaceLedger.Entries.Where(entry => entry.Value.Detail == SemanticSurfaceLedger.Unclassified).Select(entry => entry.Key).ShouldBeEmpty();

    [Fact]
    void should_not_retain_the_scaffold_placeholder() =>
        SemanticSurfaceLedger.Entries.Where(entry => entry.Value.Detail.Contains("scaffold-only", StringComparison.Ordinal)).Select(entry => entry.Key).ShouldBeEmpty();

    [Fact]
    void should_name_a_code_on_every_warned_entry() =>
        SemanticSurfaceLedger.Entries.Where(entry => entry.Value.Kind == SemanticSurfaceDispositionKind.Warned && !entry.Value.Detail.Contains("STAGE-AXON-", StringComparison.Ordinal)).Select(entry => entry.Key).ShouldBeEmpty();

    [Fact]
    void should_pin_every_unread_behavioral_member() =>
        SemanticSurfaceLedger.Entries.Where(entry => entry.Value.Detail == SemanticSurfaceLedger.UnreadBehavioral).Select(entry => entry.Key).Order().ShouldEqual(
            [.. UnreadMembers.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order()]);

    [Theory]
    [InlineData("SemanticSlice.Captures", "STAGE-AXON-0601")]
    [InlineData("SemanticCapture.Map", "STAGE-AXON-0601")]
    [InlineData("SemanticCaptureEventsSource.KeyIsEventSource", "STAGE-AXON-0601")]
    [InlineData("SemanticSliceKind.Translate", "STAGE-AXON-0601")]
    [InlineData("SemanticTranslationDirection.Inbound", "STAGE-AXON-0601")]
    [InlineData("SemanticEventContract.Visibility", "STAGE-AXON-0601")]
    [InlineData("SemanticEventVisibility.Public", "STAGE-AXON-0601")]
    [InlineData("SemanticInvocation.OnRefused", "STAGE-AXON-0506")]
    [InlineData("SemanticInvocationRefusal.Selector", "STAGE-AXON-0506")]
    public void should_warn_on_unrendered_behavior(string member, string code)
    {
        SemanticSurfaceLedger.Entries[member].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        SemanticSurfaceLedger.Entries[member].Detail.ShouldContain(code);
    }

    [Fact]
    void should_keep_private_event_visibility_rendered() =>
        SemanticSurfaceLedger.Entries["SemanticEventVisibility.Private"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rendered);

    [Fact]
    void should_render_the_reserved_atomic_mode_by_construction() =>
        SemanticSurfaceLedger.Entries["SemanticReactionProductionMode.Atomic"].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Rendered);

    [Fact]
    void should_keep_behavioral_classification_after_view_initialization() =>
        SemanticSurfaceLedger.Entries.Where(entry => entry.Key.StartsWith("SemanticCapture", StringComparison.Ordinal) && entry.Value.Kind == SemanticSurfaceDispositionKind.Rendered).ShouldBeEmpty();

    const string UnreadMembers = "SemanticGeneration.Case SemanticGeneration.Size SemanticGeneration.Alphabet SemanticGeneration.Pattern SemanticGeneration.Checksum SemanticValidationRuleKind.Checksum SemanticValidationRuleKind.RulePredicate SemanticValidationRuleKind.CodeValidation SemanticValidationRuleKind.DecimalPlaces SemanticValidationSeverity.Information SemanticEventSource.SourceKind SemanticEventStream.StreamIdParts SemanticEventStream.StreamIdType SemanticEventStream.StreamKind SemanticImplementationRequirement.BodyLines SemanticAttachmentResolution.UnresolvedFile SemanticAttachmentResolution.Pending SemanticExecutionCapability.ImplementationBody SemanticExecutionCapability.IdentityAllocation SemanticExpressionKind.ReadPath SemanticEventContextValueKind.CausedByName SemanticEventContextValueKind.CausedByUserName SemanticFixtureRoute.StreamId SemanticFixtureRoute.StreamIdParts SemanticFixtureRoutePart.Part SemanticValueKind.Null SemanticClaimTargetKind.Subject SemanticReadPathExpression.PropertyId SemanticImplementationRole.ConceptValidation SemanticImplementationRole.RulePredicate SemanticImplementationRole.PolicyPredicate SemanticImplementationRole.ConstraintPredicate SemanticImplementationRole.ValueGeneration SemanticPerformedQueryArgumentKind.Filter";
}
