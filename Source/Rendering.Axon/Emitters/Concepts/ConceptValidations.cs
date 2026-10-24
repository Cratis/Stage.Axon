// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class ConceptValidations
{
    static readonly IReadOnlyDictionary<SemanticValidationRuleKind, string> _comparisons = new Dictionary<SemanticValidationRuleKind, string>
    {
        [SemanticValidationRuleKind.Minimum] = ">=",
        [SemanticValidationRuleKind.GreaterThanOrEqual] = ">=",
        [SemanticValidationRuleKind.Maximum] = "<=",
        [SemanticValidationRuleKind.LessThanOrEqual] = "<=",
        [SemanticValidationRuleKind.GreaterThan] = ">",
        [SemanticValidationRuleKind.LessThan] = "<",
        [SemanticValidationRuleKind.Equal] = "==",
        [SemanticValidationRuleKind.NotEqual] = "!="
    };

    static readonly IReadOnlyDictionary<SemanticValidationRuleKind, string> _textMethods = new Dictionary<SemanticValidationRuleKind, string>
    {
        [SemanticValidationRuleKind.Matches] = "value.matches({value})",
        [SemanticValidationRuleKind.Contains] = "value.contains({value})",
        [SemanticValidationRuleKind.NotContains] = "!value.contains({value})",
        [SemanticValidationRuleKind.StartsWith] = "value.startsWith({value})",
        [SemanticValidationRuleKind.EndsWith] = "value.endsWith({value})",
        [SemanticValidationRuleKind.Equal] = "value.equals({value})",
        [SemanticValidationRuleKind.NotEqual] = "!value.equals({value})"
    };

    static readonly IReadOnlyDictionary<SemanticValidationRuleKind, string> _lengthComparisons = new Dictionary<SemanticValidationRuleKind, string>
    {
        [SemanticValidationRuleKind.Length] = "==",
        [SemanticValidationRuleKind.Minimum] = ">=",
        [SemanticValidationRuleKind.Maximum] = "<="
    };

    internal static void Emit(AxonEmissionContext context, SemanticConcept concept, JavaCodeBuilder body)
    {
        body.Line("java.util.Objects.requireNonNull(value, \"value\");");
        foreach (var rule in concept.Validations)
        {
            var expression = Predicate(concept.Primitive, rule);
            if (expression is not null && rule.Severity == SemanticValidationSeverity.Error)
            {
                body.Line($"if (!({expression})) throw new IllegalArgumentException({JavaCodeBuilder.StringLiteral(rule.Message ?? ("Invalid " + concept.Name + " value"))});");
                continue;
            }
            var path = concept.Id + "/validation/" + rule.Kind;
            var intent = "validate " + concept.Name + " using " + rule.Kind;
            context.Degradation.Unsupported(AxonRefusalCodes.Implementation, concept.Id, "This concept validation requires an implementation.", intent, path);
            body.UnsupportedIntent(intent, path, AxonRefusalCodes.Implementation);
            if (rule.Severity == SemanticValidationSeverity.Error)
                body.Line("if (value != null) throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral(AxonRefusalCodes.Implementation + ": " + intent) + ");");
        }
    }

    static string? Predicate(SemanticPrimitiveType primitive, SemanticValidationRule rule)
    {
        if (primitive == SemanticPrimitiveType.Text) return TextPredicate(rule);
        if (rule.Operand is not SemanticNumberValue number || primitive is not (SemanticPrimitiveType.DecimalNumber or SemanticPrimitiveType.WholeNumber)) return null;
        var literal = number.Value.ToString(CultureInfo.InvariantCulture);
        var value = primitive == SemanticPrimitiveType.DecimalNumber ? "value" : "new java.math.BigDecimal(value.toString())";
        var op = _comparisons.GetValueOrDefault(rule.Kind);
        return op is null ? null : $"{value}.compareTo(new java.math.BigDecimal(\"{literal}\")) {op} 0";
    }

    static string? TextPredicate(SemanticValidationRule rule)
    {
        if (rule.Kind == SemanticValidationRuleKind.NotEmpty) return "!value.isEmpty()";
        return rule.Operand switch
        {
            SemanticTextValue text => _textMethods.GetValueOrDefault(rule.Kind)?.Replace("{value}", JavaCodeBuilder.StringLiteral(text.Value), StringComparison.Ordinal),
            SemanticNumberValue number => TextLength(rule.Kind, number),
            _ => null
        };
    }

    static string? TextLength(SemanticValidationRuleKind kind, SemanticNumberValue number)
    {
        var op = _lengthComparisons.GetValueOrDefault(kind);
        return op is null ? null : "value.length() " + op + " " + number.Value.ToString(CultureInfo.InvariantCulture);
    }
}
