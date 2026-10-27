// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_verifying_audited_areas
{
    [Fact]
    public void should_not_leave_model_members_legacy()
    {
        const string types = "ExecutableSemanticModel LanguageVersion SemanticVersion SemanticRevision SemanticId EventContractId EventContractRevision SemanticApplication SemanticReactionProductionMode SemanticModule SemanticFeature SemanticSlice SemanticSliceKind Diagnostic DiagnosticSeverity SourceLocation DiagnosticArguments DiagnosticArgument SemanticAddress SemanticKind SemanticAddressPart SemanticAddressPartKind ApplicationIdentity SemanticSourceMapEntry SemanticSourceSpan DocumentId SemanticIdentityOrigin SemanticSourceMapRole CodeBlockSourceLine";
        Audit(types, 125);
    }

    [Fact]
    public void should_not_leave_domain_members_legacy() => Audit("SemanticConcept SemanticGeneration SemanticConceptCompliance SemanticSecret SemanticPersonalData SemanticCompositeType SemanticProperty SemanticTypeReference SemanticTypeReferenceKind SemanticPrimitiveType SemanticValidationRule SemanticValidationRuleKind SemanticValidationSeverity", 82);

    [Fact]
    public void should_not_leave_event_members_legacy() => Audit("SemanticEventSource SemanticEventStream SemanticStreamIdPart SemanticEventContract SemanticEventRevision SemanticEventVisibility", 29);

    [Fact]
    public void should_not_leave_command_members_legacy() => Audit("SemanticProducedEvent SemanticPropertyMapping SemanticStateChangeDestination SemanticResolvedExpression SemanticExpression SemanticExpressionKind SemanticRefusalExpression SemanticExpressionRootKind SemanticExpressionSourceKind SemanticEventContextExpression SemanticEventContextValueKind SemanticValueExpression SemanticRequirement SemanticComparison SemanticComparisonOperator SemanticConditionOperand SemanticLogicalCondition SemanticLogicalOperator SemanticConstraint SemanticConstraintTarget SemanticConstraintKind SemanticConstraintScope SemanticCommandResponse SemanticScalarCommandResponse SemanticRecordCommandResponse SemanticCommandResponseField SemanticSpecificationResponse SemanticCommandReadMode SemanticDecisionRead SemanticDecisionReadPresence SemanticReadPathExpression SemanticCommand SemanticCommandHandler SemanticCodeValidation SemanticCommandRoute SemanticCommandRoutePart", 111);

    [Fact]
    public void should_not_leave_security_members_legacy() => Audit("SemanticAuthenticatedCondition SemanticAnonymousCondition SemanticAuthorization SemanticCondition SemanticPolicyCondition SemanticOpaquePolicyCondition SemanticNotPolicyCondition SemanticPolicy SemanticPolicyReference SemanticClaimCondition SemanticClaimTargetKind SemanticRoleCondition SemanticLogicalAuthorization SemanticLogicalPolicyCondition", 23);

    [Fact]
    public void should_not_leave_automation_members_legacy() => Audit("SemanticApplicationTrigger SemanticInvocation SemanticInvocationRefusal SemanticReaction SemanticReactionTrigger SemanticReactionTriggerKind SemanticCapture SemanticCaptureAppend SemanticCaptureChildren SemanticCaptureNested SemanticCaptureRecord SemanticCaptureField SemanticCaptureFieldKind SemanticCaptureCondition SemanticCaptureConditionKind SemanticCaptureMap SemanticCaptureMapKind SemanticCaptureMapping SemanticCaptureTemplatePart SemanticCaptureTranslation SemanticCaptureEventsSource SemanticReactionIdentity SemanticReactionIdentityKind SemanticObserverFilter SemanticTranslationDirection", 99);

    [Fact]
    public void should_not_leave_view_members_legacy() => Audit("SemanticProjectionEventSourceIdentity SemanticProjectionTargetKind SemanticPerformedQueryShape SemanticPerformedQueryArgument SemanticPerformedQueryArgumentKind SemanticReducer SemanticReducerTransition SemanticReducerKey SemanticReducerResult SemanticReadModel SemanticProjection SemanticProjectionTransition SemanticAffectedInstance AffectedInstanceCardinality SemanticKeyedQuery SemanticQueryKeyPart SemanticQueryFilter SemanticQueryFilterValue SemanticReadModelQueryArgument SemanticQueryCardinality SemanticQueryDelivery SemanticProjectionScope SemanticProjectionChildren SemanticProjectionCompositeKey SemanticProjectionEventContextValue SemanticProjectionEventProperty SemanticProjectionEvery SemanticProjectionFrom SemanticProjectionJoin SemanticProjectionJoinRemoval SemanticProjectionKey SemanticProjectionKeyKind SemanticProjectionPartsKey SemanticProjectionKeyPart SemanticProjectionLiteral SemanticProjectionMapping SemanticProjectionNested SemanticProjectionOperation SemanticProjectionRemoval SemanticProjectionValue SemanticProjectionValueKey SemanticProjectionValueKind", 135);

    static void Audit(string types, int count)
    {
        var names = types.Split(' ').ToHashSet(StringComparer.Ordinal);
        var entries = SemanticSurfaceLedger.Entries.Where(entry => names.Contains(entry.Key.Split('.')[0])).ToArray();
        Assert.Equal(count, entries.Length);
        Assert.Empty(entries.Where(entry => entry.Value.IsLegacy).Select(entry => entry.Key));
    }
}
