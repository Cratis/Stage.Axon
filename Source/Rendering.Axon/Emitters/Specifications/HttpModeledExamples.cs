// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
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
        switch (literal)
        {
            case SemanticTextValue text: value = text.Value; return true;
            case SemanticNumberValue number: value = number.Value; return true;
            case SemanticBooleanValue boolean: value = boolean.Value; return true;
            case SemanticNullValue: return type.IsOptional;
            case SemanticArrayValue array when type.IsCollection:
                var items = new List<object?>();
                foreach (var item in array.Values)
                {
                    if (!Literal(context, type with { IsCollection = false }, item, depth + 1, out var element)) return false;
                    items.Add(element);
                }
                value = items; return true;
            case SemanticCompositeValue composite when type.Kind == SemanticTypeReferenceKind.CompositeType:
                var definition = context.Request.Model.Application.Types.First(item => item.Id == type.Target);
                value = Properties(context, definition.Properties, composite.Properties, depth + 1);
                return value is not null;
            default: return false;
        }
    }
}
