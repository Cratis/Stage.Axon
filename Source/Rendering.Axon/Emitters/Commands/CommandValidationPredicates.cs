// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandValidationPredicates(CommandJavaTypes types, SemanticValidationRule rule, SemanticTypeReference type, string access)
{
    static readonly Dictionary<SemanticValidationRuleKind, string> _textMethods = new()
    {
        [SemanticValidationRuleKind.Equal] = "equals",
        [SemanticValidationRuleKind.NotEqual] = "equals",
        [SemanticValidationRuleKind.Contains] = "contains",
        [SemanticValidationRuleKind.NotContains] = "contains",
        [SemanticValidationRuleKind.StartsWith] = "startsWith",
        [SemanticValidationRuleKind.EndsWith] = "endsWith"
    };

    static readonly Dictionary<SemanticValidationRuleKind, string> _comparisons = new()
    {
        [SemanticValidationRuleKind.Maximum] = "<=",
        [SemanticValidationRuleKind.LessThanOrEqual] = "<=",
        [SemanticValidationRuleKind.Minimum] = ">=",
        [SemanticValidationRuleKind.GreaterThanOrEqual] = ">=",
        [SemanticValidationRuleKind.GreaterThan] = ">",
        [SemanticValidationRuleKind.LessThan] = "<"
    };

    static readonly IReadOnlyDictionary<SemanticValidationRuleKind, string> _lengthComparisons = new Dictionary<SemanticValidationRuleKind, string>
    {
        [SemanticValidationRuleKind.Maximum] = "<=",
        [SemanticValidationRuleKind.Minimum] = ">=",
        [SemanticValidationRuleKind.Length] = "=="
    };

    static readonly Dictionary<SemanticPrimitiveType, Func<string, string>> _nonDefaults = new()
    {
        [SemanticPrimitiveType.Text] = value => $"!{value}.isBlank()",
        [SemanticPrimitiveType.Uuid] = value => $"!{value}.equals(new java.util.UUID(0L, 0L))",
        [SemanticPrimitiveType.WholeNumber] = value => $"{value} != 0L",
        [SemanticPrimitiveType.DecimalNumber] = value => $"{value}.signum() != 0",
        [SemanticPrimitiveType.Boolean] = value => value,
        [SemanticPrimitiveType.Date] = value => $"!{value}.equals(java.time.LocalDate.of(1, 1, 1))",
        [SemanticPrimitiveType.DateTime] = value => $"!{value}.toInstant().equals(java.time.Instant.parse(\"0001-01-01T00:00:00Z\"))"
    };

    readonly string _value = types.Unwrap(access, type);
    readonly SemanticPrimitiveType _primitive = types.PrimitiveOf(type);

    internal string? Render()
    {
        if (rule.Kind == SemanticValidationRuleKind.NotEmpty) return NotEmpty();
        if (type.IsCollection) return CollectionRule();
        var predicate = ScalarRule();
        if (rule.Kind is SemanticValidationRuleKind.Equal or SemanticValidationRuleKind.NotEqual) return predicate;
        return predicate is null ? null : $"({access} == null || ({predicate}))";
    }

    string? TextRule() => rule.Operand switch
    {
        SemanticNumberValue number when number.Value == decimal.Truncate(number.Value) => TextLength(number),
        SemanticTextValue text => TextContent(text),
        _ => null
    };

    string? TextLength(SemanticNumberValue length)
    {
        var comparison = _lengthComparisons.GetValueOrDefault(rule.Kind);
        return comparison is null ? null : $"{_value}.length() {comparison} {length.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    string? TextContent(SemanticTextValue text)
    {
        if (!_textMethods.TryGetValue(rule.Kind, out var method)) return null;
        var negation = rule.Kind is SemanticValidationRuleKind.NotEqual or SemanticValidationRuleKind.NotContains ? "!" : string.Empty;
        return $"{negation}{_value}.{method}({JavaCodeBuilder.StringLiteral(text.Value)})";
    }

    string NotEmpty()
    {
        var present = $"{access} != null";
        if (type.IsCollection) return $"({present} && !{access}.isEmpty())";
        var content = _nonDefaults.TryGetValue(_primitive, out var predicate) ? predicate(_value) : null;
        return content is null ? present : $"({present} && {content})";
    }

    string? ScalarRule()
    {
        if (rule.Kind is SemanticValidationRuleKind.Equal or SemanticValidationRuleKind.NotEqual) return Equality();
        if (_primitive == SemanticPrimitiveType.Text) return TextRule();
        var operand = Operand();
        if (operand is null) return null;
        return _comparisons.TryGetValue(rule.Kind, out var comparison) ? $"{_value}.compareTo({operand}) {comparison} 0" : null;
    }

    string? Equality()
    {
        var operand = Operand();
        if (operand is null) return null;
        var equality = EqualityComparison(operand);
        return rule.Kind == SemanticValidationRuleKind.Equal ? equality : $"!({equality})";
    }

    string? Operand() => rule.Operand is { } constant ? types.Literal(constant, type with { Kind = SemanticTypeReferenceKind.Primitive, Primitive = _primitive }) : null;

    string NullableValue() => type.Kind == SemanticTypeReferenceKind.Concept ? $"({access} == null ? null : {_value})" : access;

    string EqualityComparison(string operand)
    {
        var value = NullableValue();
        return _primitive == SemanticPrimitiveType.DecimalNumber && operand != "null"
            ? $"({value} != null && {value}.compareTo({operand}) == 0)"
            : $"java.util.Objects.equals({value}, {operand})";
    }

    string? CollectionRule()
    {
        if (rule.Kind is not (SemanticValidationRuleKind.AllGreaterThan or SemanticValidationRuleKind.AllGreaterThanOrEqual)) return null;
        var scalar = type with { IsCollection = false };
        var operand = rule.Operand is { } constant ? types.Literal(constant, scalar) : null;
        if (operand is null) return null;
        var operation = rule.Kind == SemanticValidationRuleKind.AllGreaterThan ? ">" : ">=";
        return $"({access} == null || {access}.stream().allMatch(item -> item != null && {types.Unwrap("item", scalar)}.compareTo({types.Unwrap(operand, scalar)}) {operation} 0))";
    }
}
