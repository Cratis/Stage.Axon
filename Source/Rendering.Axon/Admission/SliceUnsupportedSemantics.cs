// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Finds the unsupported Screenplay constructs inside a slice.</summary>
internal static class SliceUnsupportedSemantics
{
    internal static IEnumerable<string> Labels(object? node, HashSet<object> seen)
    {
        switch (node)
        {
            case null or string or Enum or SemanticId:
                yield break;
            case { } primitive when primitive.GetType().IsPrimitive:
                yield break;
            case System.Collections.IEnumerable items:
                foreach (var item in AxonBehaviorAdmission.Items(items)) foreach (var found in Labels(item, seen)) yield return found;
                yield break;
        }

        if (!seen.Add(node)) yield break;
        var own = Construct(node) ?? NewConstruct(node);
        if (own is not null) yield return own;
        foreach (var property in node.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0))
        {
            foreach (var found in Labels(property.GetValue(node), seen)) yield return found;
        }
    }

    static string? Construct(object node) => node switch
    {
        SemanticOneOfExpression => "one-of expression",
        SemanticClockOperand => "clock operand",
        SemanticConditionOperand { DateOf: true } => "date-only operand",
        SemanticCommandCalendar => "command calendar",
        SemanticCommandHandler { PossibleEvents.IsDefaultOrEmpty: false } => "possible events",
        SemanticSpecification { GivenTimeZone.Length: > 0 } => "specification time zone",
        _ => null
    };

    static string? NewConstruct(object node) => node switch
    {
        SemanticSlice { Screens.IsDefaultOrEmpty: false } => "screens",
        SemanticSlice { Provides.IsDefaultOrEmpty: false } => "provided context values",
        SemanticSlice { Adoptions.IsDefaultOrEmpty: false } => "screen adoptions",
        SemanticInvocation { Ability: not null } => "ability invocation",
        SemanticProjectionMapping { Operation: SemanticProjectionOperation.SetOnce } => "set once",
        SemanticProjectionChildren { Ordering: not null } => "children ordering",
        SemanticConditionOperand { CallerId: true } => "caller identity",
        SemanticComparison { Operator: SemanticComparisonOperator.Contains or SemanticComparisonOperator.StartsWith } => "text or membership comparison",
        _ => null
    };
}
