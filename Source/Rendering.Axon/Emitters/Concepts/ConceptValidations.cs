// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Concepts;

internal static class ConceptValidations
{
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
        if (rule.Operand is not SemanticNumberValue number) return null;
        var literal = number.Value.ToString(CultureInfo.InvariantCulture);
        var compare = primitive switch
        {
            SemanticPrimitiveType.DecimalNumber => $"value.compareTo(new java.math.BigDecimal(\"{literal}\"))",
            SemanticPrimitiveType.WholeNumber => $"new java.math.BigDecimal(value.toString()).compareTo(new java.math.BigDecimal(\"{literal}\"))",
            _ => null
        };
        var op = Comparison(rule.Kind);
        return compare is null || op is null ? null : $"{compare} {op} 0";
    }

    static string? TextPredicate(SemanticValidationRule rule) => rule.Kind switch
    {
        SemanticValidationRuleKind.NotEmpty => "!value.isEmpty()",
        SemanticValidationRuleKind.Matches when rule.Operand is SemanticTextValue text => "value.matches(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        SemanticValidationRuleKind.Contains when rule.Operand is SemanticTextValue text => "value.contains(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        SemanticValidationRuleKind.NotContains when rule.Operand is SemanticTextValue text => "!value.contains(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        SemanticValidationRuleKind.StartsWith when rule.Operand is SemanticTextValue text => "value.startsWith(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        SemanticValidationRuleKind.EndsWith when rule.Operand is SemanticTextValue text => "value.endsWith(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        SemanticValidationRuleKind.Length when rule.Operand is SemanticNumberValue number => "value.length() == " + number.Value.ToString(CultureInfo.InvariantCulture),
        SemanticValidationRuleKind.Minimum when rule.Operand is SemanticNumberValue number => "value.length() >= " + number.Value.ToString(CultureInfo.InvariantCulture),
        SemanticValidationRuleKind.Maximum when rule.Operand is SemanticNumberValue number => "value.length() <= " + number.Value.ToString(CultureInfo.InvariantCulture),
        SemanticValidationRuleKind.Equal when rule.Operand is SemanticTextValue text => "value.equals(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        SemanticValidationRuleKind.NotEqual when rule.Operand is SemanticTextValue text => "!value.equals(" + JavaCodeBuilder.StringLiteral(text.Value) + ")",
        _ => null
    };

    static string? Comparison(SemanticValidationRuleKind kind) => kind switch
    {
        SemanticValidationRuleKind.Minimum or SemanticValidationRuleKind.GreaterThanOrEqual => ">=",
        SemanticValidationRuleKind.Maximum or SemanticValidationRuleKind.LessThanOrEqual => "<=",
        SemanticValidationRuleKind.GreaterThan => ">",
        SemanticValidationRuleKind.LessThan => "<",
        SemanticValidationRuleKind.Equal => "==",
        SemanticValidationRuleKind.NotEqual => "!=",
        _ => null
    };
}
