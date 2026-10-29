// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal static class HttpModeledExamples
{
    internal static IEnumerable<SemanticSpecification> Specifications(AxonEmissionContext context) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Specifications);

    internal static Dictionary<string, object?>? Row(AxonEmissionContext context, SemanticReadModel model)
    {
        var specs = Specifications(context).ToArray();
        var examples = specs.SelectMany(spec => spec.ThenReadModels.Concat(spec.ThenQueries.SelectMany(query => query.Results)))
            .Concat(specs.SelectMany(spec => spec.GivenReadModels));
        foreach (var example in examples.Where(example => example.ReadModel == model.Id))
        {
            var values = example.Values.ToList();
            if (model.Properties.FirstOrDefault(property => property.IsIdentifier)?.Id is { } key && values.TrueForAll(value => value.TargetProperty != key)) values.Add(new(key, example.Key));
            var row = Properties(context, model.Properties, values, 0);
            if (row is not null) return row;
        }
        return null;
    }

    internal static bool Value(AxonEmissionContext context, SemanticTypeReference type, out object? value)
    {
        var owners = ScopeSelection.Locate(context.Request.Model.Application);
        var properties = owners.SelectMany(owner => owner.Slice.Commands.SelectMany(command => command.Properties)
            .Concat(owner.Slice.Events.SelectMany(@event => @event.Properties)).Concat(owner.Slice.ReadModels.SelectMany(model => model.Properties)));
        var ids = properties.Where(property => property.Type == type).Select(property => property.Id).ToHashSet();
        var specs = Specifications(context).ToArray();
        var examples = specs.SelectMany(spec => spec.ThenEvents.SelectMany(@event => @event.Values)
            .Concat(spec.ThenReadModels.SelectMany(model => model.Values))
            .Concat(spec.ThenQueries.SelectMany(query => query.Results).SelectMany(model => model.Values))
            .Concat(spec.When?.Values ?? []))
            .Concat(specs.SelectMany(spec => spec.GivenReadModels.SelectMany(model => model.Values).Concat(spec.GivenEvents.SelectMany(@event => @event.Values))));
        foreach (var example in examples.Where(example => ids.Contains(example.TargetProperty)))
            if (Literal(context, type, example.Value, 0, out value)) return true;
        value = null;
        return false;
    }

    internal static bool Literal(AxonEmissionContext context, SemanticTypeReference type, SemanticValue literal, out object? value) => Literal(context, type, literal, 0, out value);

    static Dictionary<string, object?>? Properties(AxonEmissionContext context, IEnumerable<SemanticProperty> properties, IEnumerable<SemanticPropertyValue> values, int depth)
    {
        var row = new Dictionary<string, object?>();
        foreach (var property in properties)
        {
            var supplied = values.FirstOrDefault(value => value.TargetProperty == property.Id);
            if (supplied is null || !Literal(context, property.Type, supplied.Value, depth, out var value)) return null;
            row.Add(JavaNames.Member(property.Name), value);
        }
        return row;
    }

    static bool Literal(AxonEmissionContext context, SemanticTypeReference type, SemanticValue literal, int depth, out object? value)
    {
        value = null;
        if (depth > 8) return false;
        if (literal is SemanticNullValue) return type.IsOptional;
        if (type.IsCollection)
        {
            if (literal is not SemanticArrayValue array) return false;
            var items = new List<object?>();
            foreach (var item in array.Values)
            {
                if (!Literal(context, type with { IsCollection = false, IsOptional = false }, item, depth + 1, out var element)) return false;
                items.Add(element);
            }
            value = items;
            return true;
        }
        if (type.Kind == SemanticTypeReferenceKind.CompositeType)
        {
            if (literal is not SemanticCompositeValue composite) return false;
            var definition = context.Request.Model.Application.Types.First(item => item.Id == type.Target);
            value = Properties(context, definition.Properties, composite.Properties, depth + 1);
            return value is not null;
        }
        if (!Scalar(context, type, literal)) return false;
        value = literal switch
        {
            SemanticTextValue text => text.Value,
            SemanticNumberValue number => number.Value,
            SemanticBooleanValue boolean => boolean.Value,
            _ => null
        };
        return value is not null;
    }

    static bool Scalar(AxonEmissionContext context, SemanticTypeReference type, SemanticValue literal)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application, context.BasePackage);
        if (type.Kind != SemanticTypeReferenceKind.Concept) return Primitive(types.PrimitiveOf(type), literal);
        var concept = context.Request.Model.Application.Concepts.First(item => item.Id == type.Target);
        if (!concept.Values.IsDefaultOrEmpty)
        {
            return ConceptEnums.IsNumeric(concept)
                ? literal is SemanticNumberValue number && concept.Numbers is { } numbers && numbers.Any(value => value == number.Value)
                : literal is SemanticTextValue text && text.Value?.Any(char.IsSurrogate) == false && concept.Values.Contains(text.Value, StringComparer.Ordinal);
        }
        if (concept.Compliance is { Personal: not null } or { Secret: not null }) return false;
        if (!Primitive(concept.Primitive, literal)) return false;

        // Only establish the predicates below. Other rules (including implementation bodies)
        // are not evidence that a modeled literal can pass the generated constructor.
        return concept.Validations.IsDefaultOrEmpty ||
            (concept.Primitive == SemanticPrimitiveType.Text && literal is SemanticTextValue textValue && concept.Validations.All(rule => TextRule(textValue.Value, rule)));
    }

    static bool Primitive(SemanticPrimitiveType primitive, SemanticValue literal) => (primitive, literal) switch
    {
        (SemanticPrimitiveType.Text, SemanticTextValue text) => text.Value?.Any(char.IsSurrogate) == false,
        (SemanticPrimitiveType.Boolean, SemanticBooleanValue) => true,
        (SemanticPrimitiveType.WholeNumber, SemanticNumberValue number) => number.Value == decimal.Truncate(number.Value) && number.Value >= long.MinValue && number.Value <= long.MaxValue,
        (SemanticPrimitiveType.DecimalNumber, SemanticNumberValue) => true,
        (SemanticPrimitiveType.Uuid, SemanticTextValue text) => Guid.TryParseExact(text.Value, "D", out _),
        (SemanticPrimitiveType.Date, SemanticTextValue text) => DateOnly.TryParseExact(text.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        (SemanticPrimitiveType.DateTime, SemanticTextValue text) => DateTimeOffset.TryParseExact(text.Value, ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss'Z'"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
        _ => false
    };

    static bool TextRule(string value, SemanticValidationRule rule)
    {
        if (rule.Severity != SemanticValidationSeverity.Error) return false;
        if (rule.Kind == SemanticValidationRuleKind.NotEmpty) return value.Length > 0;
        if (rule.Operand is SemanticNumberValue number && number.Value == decimal.Truncate(number.Value) && number.Value >= 0 && number.Value <= int.MaxValue)
        {
            return rule.Kind switch
            {
                SemanticValidationRuleKind.Length => value.Length == number.Value,
                SemanticValidationRuleKind.Minimum => value.Length >= number.Value,
                SemanticValidationRuleKind.Maximum => value.Length <= number.Value,
                _ => false
            };
        }
        if (rule.Operand is not SemanticTextValue text) return false;
        return rule.Kind switch
        {
            SemanticValidationRuleKind.Contains => value.Contains(text.Value, StringComparison.Ordinal),
            SemanticValidationRuleKind.NotContains => !value.Contains(text.Value, StringComparison.Ordinal),
            SemanticValidationRuleKind.StartsWith => value.StartsWith(text.Value, StringComparison.Ordinal),
            SemanticValidationRuleKind.EndsWith => value.EndsWith(text.Value, StringComparison.Ordinal),
            SemanticValidationRuleKind.Equal => value.Equals(text.Value, StringComparison.Ordinal),
            SemanticValidationRuleKind.NotEqual => !value.Equals(text.Value, StringComparison.Ordinal),
            SemanticValidationRuleKind.Matches => PortableMatch(value, text.Value),
            _ => false
        };
    }

    static bool PortableMatch(string value, string pattern)
    {
        // A deliberately small common subset of Java String.matches and .NET regex:
        // ASCII literals/classes (only a-z, A-Z, 0-9 ranges; literal hyphens last)
        // and + or counted repetition, with full-string matching. No anchors,
        // escapes, groups, intersections, flags or Unicode-dependent behavior.
        const string grammar = @"\A(?:(?:[A-Za-z0-9@_-]|\[(?:a-z|A-Z|0-9|[A-Za-z0-9._%+@])+-?\])(?:\+|\{[0-9]{1,3}(?:,[0-9]{0,3})?\})?)+\z";
        if (value.Length > 4096 || pattern.Length > 256 || value.Any(character => character < ' ' || character > '~')) return false;
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        var timeout = TimeSpan.FromMilliseconds(50);
        try
        {
            return Regex.IsMatch(pattern, grammar, options, timeout) && Regex.IsMatch(value, @"\A(?:" + pattern + @")\z", options, timeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
