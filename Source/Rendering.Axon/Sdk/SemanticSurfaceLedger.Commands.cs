// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Events;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static partial class SemanticSurfaceLedger
{
    static void ClassifyCommandMembers(LedgerBuilder rows)
    {
        rows.Rendered("Command identity, name and fields supply the generated command contract and artifact attribution.", "SemanticCommand.Id SemanticCommand.Name SemanticCommand.Properties", typeof(CommandContractsEmitter));
        rows.Rendered("Append mappings resolve the target event property and generate its source expression.", "SemanticPropertyMapping.Source SemanticPropertyMapping.TargetProperty", typeof(CommandAppends));
        rows.Rendered("The destination type resolves the concept used for the primary event-source tag.", "SemanticStateChangeDestination.Type", typeof(AxonEventSources));
        rows.Rendered("The destination expression selects the source value for generated appends.", "SemanticStateChangeDestination.Value", typeof(CommandSource));
        rows.Rendered("Command property references resolve and convert the referenced command field.", "SemanticResolvedExpression.Root SemanticResolvedExpression.Source SemanticResolvedExpression.Target", typeof(CommandExpressions));
        rows.Rendered("The command root and property-source tag select supported command field expressions.", "SemanticExpressionRootKind.Command SemanticExpressionSourceKind.Property", typeof(CommandExpressions));
        rows.Rendered("Event and application-trigger roots select supported reaction input references.", "SemanticExpressionRootKind.Event SemanticExpressionRootKind.Trigger", typeof(ReactionInput));
        rows.Rendered("Literal expressions supply values converted to the modeled mapping target type.", "SemanticValueExpression.Value", typeof(CommandExpressions));
        rows.Rendered("Event context values select occurrence time, primary source identity or current caller subject when constructing appended events.", "SemanticEventContextExpression.Value SemanticEventContextValueKind.Occurred SemanticEventContextValueKind.EventSourceIdentity SemanticEventContextValueKind.CausedBySubject", typeof(CommandAppends));
        rows.Rendered("Command comparisons select operand expressions and the emitted comparison operator.", "SemanticComparison.Left SemanticComparison.Operator SemanticComparison.Right SemanticComparisonOperator.Equal SemanticComparisonOperator.NotEqual SemanticComparisonOperator.GreaterThan SemanticComparisonOperator.GreaterThanOrEqual SemanticComparisonOperator.LessThan SemanticComparisonOperator.LessThanOrEqual SemanticConditionOperand.Property SemanticConditionOperand.Value", typeof(CommandConditions));
        rows.Rendered("Logical command conditions compose the emitted predicates with the declared boolean operator.", "SemanticLogicalCondition.Left SemanticLogicalCondition.Operator SemanticLogicalCondition.Right SemanticLogicalOperator.And SemanticLogicalOperator.Or", typeof(CommandConditions));
        rows.Warned(["STAGE-AXON-0110"], "STAGE-AXON-0110: expression dispatch uses concrete types, not the Kind getter or these enum values. Supported concrete patterns are handled separately; unsupported append expressions cause the command handler to degrade. No independent implementation of the Kind selector is claimed.", "SemanticExpression.Kind SemanticExpressionKind.Resolved SemanticExpressionKind.Refusal SemanticExpressionKind.Value SemanticExpressionKind.EventContext");
        rows.Warned(["STAGE-AXON-0110"], "STAGE-AXON-0110: event context source Type is not consumed by append expression generation, which uses the mapping target type. Unsupported context values return no append expression and degrade at the owning command. A redundant source-type annotation alone does not guarantee a diagnostic.", "SemanticEventContextExpression.Type");
        rows.Warned(["STAGE-AXON-0110"], "STAGE-AXON-0110: CausedByName and CausedByUserName are not implemented. The append expression switch falls through and the owning command degrades rather than claiming those caller fields are rendered.", "SemanticEventContextValueKind.CausedByName SemanticEventContextValueKind.CausedByUserName");
        rows.Warned(["STAGE-AXON-0107", "STAGE-AXON-0110"], "STAGE-AXON-0107: command conditions with a protected-read operand return no predicate and report unsupported validation. STAGE-AXON-0110: read-path expressions are not supported append expressions and degrade at the owning handler. Neither the Read nor PropertyId fields of a read-path expression are implemented.", "SemanticConditionOperand.Read SemanticReadPathExpression.Read SemanticReadPathExpression.PropertyId SemanticExpressionKind.ReadPath");
        rows.Ignored(IgnoredReason.UnknownSentinel, "Unknown expression, root, source and context-value sentinels select no executable expression case; unsupported owners report their own degradation.", "SemanticExpressionKind.Unknown SemanticExpressionRootKind.Unknown SemanticExpressionSourceKind.Unknown SemanticEventContextValueKind.Unknown");
    }
}
