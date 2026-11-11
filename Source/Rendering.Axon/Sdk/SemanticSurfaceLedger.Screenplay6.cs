// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyScreenplaySix(LedgerBuilder rows)
    {
        rows.Warned(["STAGE-AXON-UNSUPPORTED-SEMANTICS"], "STAGE-AXON-UNSUPPORTED-SEMANTICS: state machines, lifecycle specifications, workflows, graph declarations, clock and calendar operands, one-of expressions, possible events and time zones are not rendered by any Axon emitter; admission reports each use at its owner." + DegradationModes, """
            SemanticValidationRule.ClockOperand SemanticClockOperand.Kind SemanticClockKind.Today SemanticClockKind.Now SemanticClockOperand.Amount SemanticClockOperand.Unit
            SemanticClockUnit.Day SemanticClockUnit.Hour SemanticClockUnit.Minute SemanticClockOperand.DateOf SemanticExpressionKind.OneOf SemanticOneOfExpression.CollectionProperty
            SemanticConditionOperand.Clock SemanticConditionOperand.DateOf SemanticCommandHandler.PossibleEvents SemanticCommand.Calendar SemanticCommandCalendar.TimeZone
            SemanticCommandCalendar.ContextMember SemanticSpecification.GivenTimeZone SemanticApplication.StateMachines SemanticStateMachine.Name SemanticStateMachine.ReadModel
            SemanticStateMachine.States SemanticState.Name SemanticState.IsInitial SemanticState.IsFinal SemanticState.Entering SemanticStateEntry.Kind
            SemanticStateEntryKind.SendCommand SemanticStateEntryKind.StartWorkflow SemanticStateEntry.Command SemanticStateEntry.Workflow SemanticStateMachine.Transitions
            SemanticStateTransition.From SemanticStateTransition.Event SemanticStateTransition.Guard SemanticStateTransition.To SemanticStateMachine.Specifications
            SemanticLifecycleSpecification.Name SemanticLifecycleSpecification.Given SemanticLifecycleSpecification.WhenAppended SemanticLifecycleSpecification.WhenStarted
            SemanticLifecycleSpecification.Outcomes SemanticLifecycleOutcome.Node SemanticLifecycleOutcome.Outlet SemanticLifecycleSpecification.Then
            SemanticLifecycleExpectation.Kind SemanticLifecycleExpectationKind.State SemanticLifecycleExpectationKind.Sends SemanticLifecycleExpectationKind.Starts
            SemanticLifecycleExpectationKind.Completes SemanticLifecycleExpectationKind.Fails SemanticLifecycleExpectationKind.Waits SemanticLifecycleExpectation.Target
            SemanticLifecycleExpectation.Command SemanticApplication.Workflows SemanticWorkflow.Name SemanticWorkflow.Subject SemanticWorkflow.Nodes SemanticWorkflowNode.Name
            SemanticWorkflowNode.Kind SemanticWorkflowNodeKind.Trigger SemanticWorkflowNodeKind.Action SemanticWorkflowNodeKind.Decision SemanticWorkflowNodeKind.Wait
            SemanticWorkflowNodeKind.Approval SemanticWorkflowNodeKind.ForEach SemanticWorkflowNodeKind.Join SemanticWorkflowNode.TriggerKind SemanticWorkflowTriggerKind.Event
            SemanticWorkflowTriggerKind.StateEntered SemanticWorkflowTriggerKind.Schedule SemanticWorkflowTriggerKind.Manual SemanticWorkflowNode.Event
            SemanticWorkflowNode.StateMachine SemanticWorkflowNode.State SemanticWorkflowNode.Every SemanticWorkflowNode.At SemanticWorkflowNode.OnDayOfWeek
            SemanticWorkflowNode.OnDayOfMonth SemanticWorkflowNode.Command SemanticWorkflowNode.MaxAttempts SemanticWorkflowNode.AttemptTimeout SemanticWorkflowNode.RetryDelay
            SemanticWorkflowNode.Condition SemanticWorkflowNode.WhenTrue SemanticWorkflowNode.WhenFalse SemanticWorkflowNode.Duration SemanticWorkflowNode.ExpiresAfter
            SemanticWorkflowNode.ApproverKind SemanticApproverKind.Persona SemanticApproverKind.Policy SemanticWorkflowNode.Approver SemanticWorkflowNode.Items
            SemanticWorkflowNode.MaxParallel SemanticWorkflowNode.JoinMode SemanticJoinMode.All SemanticJoinMode.Any SemanticJoinMode.AtLeast SemanticWorkflowNode.JoinCount
            SemanticWorkflow.Edges SemanticWorkflowEdge.From SemanticWorkflowEdge.Outlet SemanticWorkflowEdge.To SemanticWorkflow.Specifications SemanticApplication.Graphs
            SemanticGraph.Name SemanticGraph.Nodes SemanticGraphNode.Label SemanticGraphNode.Properties SemanticGraphProperty.Name SemanticGraphProperty.Type
            SemanticGraphProperty.IsKey SemanticGraph.Edges SemanticGraphEdge.Type SemanticGraphEdge.Source SemanticGraphEdge.Target SemanticGraphEdge.Properties
            SemanticGraphEdge.IdentifiedBy SemanticApplication.GraphProjections SemanticGraphProjection.Name SemanticGraphProjection.Graph SemanticGraphProjection.Nodes
            SemanticGraphElementHandlers.Name SemanticGraphElementHandlers.Handlers SemanticGraphHandler.Event SemanticGraphHandler.Mappings SemanticGraphMapping.Property
            SemanticGraphMapping.Operation SemanticGraphMapping.Source SemanticGraphMapping.Factor SemanticGraphHandler.Key SemanticGraphHandler.Source SemanticGraphHandler.Target
            SemanticGraphHandler.Identity SemanticGraphHandler.Removes SemanticGraphHandler.ForEach SemanticGraphHandler.Detach SemanticGraphProjection.Edges
            SemanticApplication.GraphSpecifications SemanticGraphSpecification.Owner SemanticGraphSpecification.Name SemanticGraphSpecification.Given
            SemanticGraphSpecification.ThenQueries SemanticGraphQueryExpectation.Query SemanticGraphQueryExpectation.Exactly SemanticGraphQueryExpectation.Arguments
            SemanticGraphPropertyValue.Property SemanticGraphPropertyValue.Value SemanticGraphQueryExpectation.Rows SemanticGraphRow.Values SemanticGraphSpecification.Clock
            SemanticGraphSpecification.GivenGraphs SemanticGraphStep.Graph SemanticGraphStep.Elements SemanticGraphElementStep.IsNode SemanticGraphElementStep.Absent
            SemanticGraphElementStep.Name SemanticGraphElementStep.Key SemanticGraphElementStep.Values SemanticGraphElementStep.Target SemanticGraphElementStep.Identity
            SemanticGraphSpecification.WhenAppended SemanticGraphSpecification.ThenGraphs SemanticApplication.GraphQueries SemanticGraphQuery.Name SemanticGraphQuery.Graph
            SemanticGraphQuery.ReadModel SemanticGraphQuery.Cardinality SemanticGraphQuery.Delivery SemanticGraphQuery.Arguments SemanticGraphQueryArgument.Name
            SemanticGraphQueryArgument.Type SemanticGraphQueryArgument.IsKey SemanticGraphQuery.Matches SemanticGraphMatch.IsOptional SemanticGraphMatch.Start
            SemanticGraphPatternNode.Alias SemanticGraphPatternNode.Label SemanticGraphMatch.Hops SemanticGraphHop.Alias SemanticGraphHop.Type SemanticGraphHop.Outgoing
            SemanticGraphHop.Node SemanticGraphQuery.Conditions SemanticGraphCondition.Alias SemanticGraphCondition.Property SemanticGraphCondition.Argument
            SemanticGraphCondition.Literal SemanticGraphQuery.Returns SemanticGraphReturn.Property SemanticGraphReturn.Alias SemanticGraphReturn.Source
            SemanticGraphQuery.Authorization SemanticApplication.TimeZone SemanticTimeZone.Id SemanticTimeZone.Configurable SemanticApplication.ContextMembers
            """);
        rows.Ignored(IgnoredReason.Advisory, "Property declaration order is presentation metadata; generated read-model members keep their declared order.", "SemanticReadModel.PropertyOrder");
    }
}
