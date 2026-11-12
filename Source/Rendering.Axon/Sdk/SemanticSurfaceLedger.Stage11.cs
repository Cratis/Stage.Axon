// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyStageEleven(LedgerBuilder rows)
    {
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: no screen, agent or external-system emitter; AxonUnsupportedSemantics reports their presence.", """
            SemanticApplication.Agents SemanticApplication.Systems SemanticSlice.Screens
            SemanticScreen.Id SemanticScreen.Name SemanticScreen.Title SemanticScreen.Data SemanticScreen.Actions SemanticScreen.Navigation SemanticScreen.Layout SemanticScreen.Description
            SemanticScreenData.Query SemanticScreenData.Fields SemanticScreenData.By
            SemanticScreenField.Property SemanticScreenField.Label
            SemanticScreenAction.Command SemanticScreenAction.Label SemanticScreenAction.Subject SemanticScreenAction.Inputs SemanticScreenAction.When SemanticScreenAction.Then
            SemanticScreenInput.Property SemanticScreenInput.From
            SemanticScreenNavigation.Screen SemanticScreenNavigation.Trigger SemanticScreenNavigation.By SemanticScreenNavigation.Opens
            SemanticScreenLayout.Fits SemanticScreenLayout.Regions
            SemanticScreenRegion.Kind SemanticScreenRegion.Regions SemanticScreenRegion.Name SemanticScreenRegion.Shows SemanticScreenRegion.Title SemanticScreenRegion.Actions
            SemanticSystem.Id SemanticSystem.Name SemanticSystem.Abilities
            SemanticAbility.Name SemanticAbility.Effect SemanticAbility.Inputs SemanticAbility.Returns SemanticAbility.Outcomes SemanticAbility.AnswersLater SemanticAbility.Realization
            SemanticAbilityEffect.Safe SemanticAbilityEffect.Idempotent SemanticAbilityEffect.Once
            SemanticAbilityField.Name SemanticAbilityField.Type
            SemanticAbilityRealization.Kind SemanticAbilityRealization.RequirementId
            SemanticAbilityRealizationKind.None SemanticAbilityRealizationKind.Code SemanticAbilityRealizationKind.Extension
            SemanticAgents.TokenKind SemanticAgents.Operations SemanticAgentTokenKind.Personal
            SemanticAgentOperation.Kind SemanticAgentOperation.Operation SemanticAgentOperation.Scope SemanticAgentOperation.Description
            SemanticAgentOperationKind.Query SemanticAgentOperationKind.Command SemanticAgentScope.Read SemanticAgentScope.Write
            SemanticImplementationRole.AbilityEffect SemanticKind.Screen SemanticKind.System
            """);
        rows.Warned(["STAGE-AXON-AUTHORIZATION"], "STAGE-AXON-AUTHORIZATION: state/typed/mapped policies deny access at endpoint and handler; row narrowing denies even without Authorization.", """
            SemanticKeyedQuery.Narrowing
            SemanticPolicy.Inputs SemanticPolicy.Reads
            SemanticPolicyInput.Name SemanticPolicyInput.Type
            SemanticPolicyInputMapping.Input SemanticPolicyInputMapping.Property
            SemanticPolicyRead.Name SemanticPolicyRead.ReadModel SemanticPolicyRead.By
            SemanticPolicyReference.Mappings
            SemanticStatePolicyCondition.Condition SemanticCaller.Id
            """);
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: AxonUnsupportedSemantics reports ordering, set-once, caller identity and text conditions; caller identity cannot fall through to a literal.", """
            SemanticProjectionChildren.Ordering
            SemanticProjectionChildrenOrdering.Direction SemanticProjectionChildrenOrdering.Placement SemanticProjectionChildrenOrdering.SortBy
            SemanticChildrenPlacement.Append SemanticChildrenPlacement.Prepend SemanticChildrenPlacement.Sorted SemanticChildrenPlacement.Unknown
            SemanticSortDirection.Ascending SemanticSortDirection.Descending SemanticSortDirection.Unknown
            SemanticProjectionOperation.SetOnce
            SemanticConditionOperand.CallerId
            SemanticComparisonOperator.Contains SemanticComparisonOperator.StartsWith
            """);
    }
}
