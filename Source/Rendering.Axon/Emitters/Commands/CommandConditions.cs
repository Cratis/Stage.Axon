// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandConditions(SemanticCommand command, CommandJavaTypes types)
{
    static readonly IReadOnlyDictionary<SemanticComparisonOperator, string> _operations = new Dictionary<SemanticComparisonOperator, string>
    {
        [SemanticComparisonOperator.Equal] = "==",
        [SemanticComparisonOperator.NotEqual] = "!=",
        [SemanticComparisonOperator.GreaterThan] = ">",
        [SemanticComparisonOperator.GreaterThanOrEqual] = ">=",
        [SemanticComparisonOperator.LessThan] = "<",
        [SemanticComparisonOperator.LessThanOrEqual] = "<="
    };

    static readonly IReadOnlyDictionary<SemanticLogicalOperator, string> _logicalOperations = new Dictionary<SemanticLogicalOperator, string>
    {
        [SemanticLogicalOperator.And] = "&&",
        [SemanticLogicalOperator.Or] = "||"
    };

    internal string? Render(SemanticCondition condition)
    {
        if (condition is SemanticLogicalCondition logical) return Logical(logical);
        return condition is SemanticComparison comparison ? Comparison(comparison) : null;
    }

    static string? Numeric(string left, string right, SemanticComparisonOperator comparison)
    {
        var operation = _operations.GetValueOrDefault(comparison);
        if (operation is null) return null;

        // Object casts also box literal operands, so null checks remain legal Java for constants.
        var present = $"(Object)({left}) != null && (Object)({right}) != null";
        var ordered = $"new java.math.BigDecimal(String.valueOf({left})).compareTo(new java.math.BigDecimal(String.valueOf({right}))) {operation} 0";
        if (comparison == SemanticComparisonOperator.Equal) return $"(java.util.Objects.equals({left}, {right}) || ({present} && {ordered}))";
        if (comparison == SemanticComparisonOperator.NotEqual) return $"(!java.util.Objects.equals({left}, {right}) && (!({present}) || {ordered}))";
        return $"({present} && {ordered})";
    }

    static string? CompareOperands(string left, string right, SemanticPrimitiveType primitive, SemanticComparisonOperator comparison)
    {
        if (primitive is SemanticPrimitiveType.DecimalNumber or SemanticPrimitiveType.WholeNumber) return Numeric(left, right, comparison);
        if (comparison is not (SemanticComparisonOperator.Equal or SemanticComparisonOperator.NotEqual)) return null;
        var equal = $"java.util.Objects.equals({left}, {right})";
        return comparison == SemanticComparisonOperator.Equal ? equal : $"!({equal})";
    }

    string? Logical(SemanticLogicalCondition logical)
    {
        var left = Render(logical.Left);
        var right = Render(logical.Right);
        var operation = _logicalOperations.GetValueOrDefault(logical.Operator);
        return left is null || right is null || operation is null ? null : $"({left} {operation} {right})";
    }

    string? Comparison(SemanticComparison comparison)
    {
        if (comparison.Left.Read is not null || comparison.Right.Read is not null) return null;
        var type = Property(comparison.Left)?.Type ?? Property(comparison.Right)?.Type;
        if (type?.IsCollection != false) return null;
        var left = Operand(comparison.Left, type);
        var right = Operand(comparison.Right, type);
        if (left is null || right is null) return null;
        return CompareOperands(left, right, types.PrimitiveOf(type), comparison.Operator);
    }

    string? Operand(SemanticConditionOperand operand, SemanticTypeReference type)
    {
        var scalar = type with { Kind = SemanticTypeReferenceKind.Primitive, Primitive = types.PrimitiveOf(type) };
        if (operand.Value is { } constant) return types.Literal(constant, scalar);
        if (Property(operand) is not { } property) return null;
        var access = $"command.{JavaNames.Member(property.Name)}()";
        return property.Type.Kind == SemanticTypeReferenceKind.Concept ? $"({access} == null ? null : {access}.value())" : access;
    }

    SemanticProperty? Property(SemanticConditionOperand operand) => command.Properties.SingleOrDefault(property => property.Id == operand.Property);
}
