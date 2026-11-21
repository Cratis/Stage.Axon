// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyScreenplay632(LedgerBuilder rows)
    {
        rows.Warned(["STAGE-AXON-AUTHORIZATION", "STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-AUTHORIZATION: availability gates are not rendered; every command and query under a module, feature, slice or own gate is denied (denyAll / empty @Secured) and one authorization degradation is reported per owner. STAGE-AXON-UNSUPPORTED-SEMANTICS reports declared audiences and availabilities." + DegradationModes, """
            SemanticApplication.Audiences SemanticApplication.Availabilities SemanticAudience.Name SemanticAudienceRequirement.Audience
            SemanticAvailability.Name SemanticAvailability.Requirements
            SemanticAvailabilityComparison.Operand SemanticAvailabilityComparison.Operator SemanticAvailabilityComparison.Path
            SemanticAvailabilityCondition.$type SemanticAvailabilityReference.Availability SemanticAvailabilityRequirement.$type
            SemanticCommand.Available SemanticConditionRequirement.Condition SemanticFeature.Available SemanticKeyedQuery.Available
            SemanticLogicalAvailability.Left SemanticLogicalAvailability.Operator SemanticLogicalAvailability.Right
            SemanticLogicalAvailabilityCondition.Left SemanticLogicalAvailabilityCondition.Operator SemanticLogicalAvailabilityCondition.Right
            SemanticModule.Available SemanticSlice.Available
            """);
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: there is no context engine; AxonUnsupportedSemantics reports provided context values (and their context operands) at the application, module, feature and slice." + DegradationModes, """
            SemanticApplication.Provides SemanticFeature.Provides SemanticModule.Provides SemanticSlice.Provides
            SemanticProvidedValue.Collection SemanticProvidedValue.Name SemanticProvidedValue.Optional SemanticProvidedValue.ReadModel SemanticProvidedValue.Source SemanticProvidedValue.Type
            SemanticProvideSource.$type SemanticQueryProvideSource.By SemanticQueryProvideSource.Query SemanticValueProvideSource.Value SemanticHostProvideSource.$type SemanticContextOperand.Path SemanticContextOperand.Value
            """);
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: there is no ability runtime; AxonUnsupportedSemantics reports state machines and workflows (and the ability invocations in their entries and nodes) as unsupported." + DegradationModes, """
            SemanticStateEntry.Ability SemanticStateEntry.Inputs SemanticStateEntry.RequirementId SemanticStateEntry.System
            SemanticStateEntryKind.InvokeAbility SemanticStateEntryKind.RunBody
            SemanticWorkflowNode.Ability SemanticWorkflowNode.Inputs SemanticWorkflowNode.RequirementId SemanticWorkflowNode.Returns SemanticWorkflowNode.System
            SemanticWorkflowNodeKind.Compute SemanticLifecycleExpectationKind.Invokes
            """);
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS", "STAGE-AXON-0504"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: ability invocations are reported by AxonUnsupportedSemantics; STAGE-AXON-0504: a reaction whose invocation targets an ability is not registered." + DegradationModes, """
            SemanticInvocation.Ability SemanticInvocation.Branches SemanticInvocation.Inputs SemanticInvocation.System
            SemanticInvocationBranch.Produces SemanticInvocationBranch.Selector
            SemanticInvocationValueExpression.Member SemanticInvocationValueExpression.Source SemanticInvocationValueExpression.Type
            SemanticExpressionKind.InvocationValue
            """);
        rows.Warned(["STAGE-AXON-SPEC"], "STAGE-AXON-SPEC: ability answers, ability invocation assertions and exact ordered event tags are not verified; scoped specification emission reports this unsupported assertion. " + SpecificationDetail, """
            SemanticSpecification.GivenAbilityAnswers SemanticSpecificationInvocation.Ability SemanticSpecificationInvocation.Inputs SemanticSpecificationInvocation.System
            SemanticAbilityAnswer.Ability SemanticAbilityAnswer.Kind SemanticAbilityAnswer.Outcome SemanticAbilityAnswer.Reason SemanticAbilityAnswer.System SemanticAbilityAnswer.Values
            SemanticAbilityAnswerKind.Failed SemanticAbilityAnswerKind.Outcome SemanticAbilityAnswerKind.Returned
            SemanticAbilityFieldValue.Name SemanticAbilityFieldValue.Value SemanticAbilityInputMapping.Input SemanticAbilityInputMapping.Source
            SemanticSpecificationEvent.ExpectedTags
            """);
        rows.Warned(["STAGE-AXON-IMPL"], "STAGE-AXON-IMPL: no inline body slot places this implementation role; the central behavior admission reports each unconsumed requirement at its source." + DegradationModes, "SemanticImplementationRole.StateEntryBody SemanticImplementationRole.WorkflowCompute");
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: no screen emitter; AxonUnsupportedSemantics reports application and slice screens and screen adoptions." + DegradationModes, """
            SemanticApplication.Screens SemanticSlice.Adoptions
            SemanticScreen.Available SemanticScreen.Components SemanticScreen.Extensions SemanticScreen.Needs SemanticScreen.Scope
            SemanticScreenAdoption.Provisions SemanticScreenAdoption.Screen
            SemanticScreenComponent.Component SemanticScreenComponent.Handles SemanticScreenComponent.Name SemanticScreenComponent.Properties SemanticScreenComponent.System
            SemanticScreenExtension.Reference SemanticScreenExtension.System
            SemanticScreenNeed.Fields SemanticScreenNeed.Many SemanticScreenNeed.Name SemanticScreenNeed.Shape
            SemanticScreenProvision.By SemanticScreenProvision.Fields SemanticScreenProvision.Need SemanticScreenProvision.Query
            SemanticScreenProvisionField.Field SemanticScreenProvisionField.Path SemanticScreenSignalHandler.Commands SemanticScreenSignalHandler.Signal
            """);
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: graph queries are reported by AxonUnsupportedSemantics, including their collected rows." + DegradationModes, """
            SemanticGraphCollect.Cardinality SemanticGraphCollect.From SemanticGraphCollect.Hop SemanticGraphCollect.Members SemanticGraphCollect.Property SemanticGraphCollect.Row
            SemanticGraphCollectCardinality.Many SemanticGraphCollectCardinality.One
            SemanticGraphCollectMember.Kind SemanticGraphCollectMember.Property SemanticGraphCollectMember.Source
            SemanticGraphCollectMemberKind.EdgeProperty SemanticGraphCollectMemberKind.Property SemanticGraphQuery.Collects
            """);
    }
}
