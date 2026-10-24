// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandFixtureValues(AxonEmissionContext context, SemanticCommand command, SemanticSpecification specification, CommandSource? source)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application);

    internal string? Command()
    {
        var values = command.Properties.Select(CommandProperty).ToList();
        if (source?.Allocated == true) values.Insert(0, specification.When?.EventSource is { } identity ? Identity(identity.Value, source.JavaType) : null);
        return values.Exists(value => value is null) ? null : $"new {JavaNames.Type(command.Name)}({string.Join(", ", values)})";
    }

    internal string? Event(SemanticSpecificationEvent occurrence)
    {
        var contract = CommandAppends.Contract(context, occurrence.EventContract);
        var primary = AxonEventSources.PrimaryTag(context, contract);
        var arguments = contract.Properties.Select(property => EventProperty(occurrence, property, primary)).ToList();
        if (!contract.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName)) arguments.Insert(0, EventIdentity(occurrence, primary));
        return arguments.Exists(value => value is null) ? null : $"new {AxonEventsEmitter.TypeFor(context, contract.Id)}({string.Join(", ", arguments)})";
    }

    internal SemanticValue? Input(SemanticProperty property) => specification.When!.Values.Concat(specification.When.GeneratedValues)
        .Concat(specification.GivenGenerated.Where(value => value.Command == command.Id).Select(value => new SemanticPropertyValue(value.Property, value.Value)))
        .LastOrDefault(value => value.TargetProperty == property.Id)?.Value;

    string? CommandProperty(SemanticProperty property)
    {
        var value = Input(property);
        if (value is not null) return _types.Literal(value, property.Type);
        if (source?.Input?.Id == property.Id && specification.When?.EventSource is { } identity) return _types.Literal(identity.Value, property.Type);
        return property.IsGenerated ? null : "null";
    }

    string? EventProperty(SemanticSpecificationEvent occurrence, SemanticProperty property, (string ComponentName, string JavaType, string TagKey) primary)
    {
        var value = occurrence.Values.FirstOrDefault(value => value.TargetProperty == property.Id)?.Value;
        if (JavaNames.Member(property.Name) == primary.ComponentName && occurrence.EventSource is { } identity)
        {
            if (value is not null && value != identity.Value) return null;
            return _types.Literal(identity.Value, property.Type);
        }
        if (value is not null) return _types.Literal(value, property.Type);
        return property.Type.IsOptional ? "null" : null;
    }

    string? EventIdentity(SemanticSpecificationEvent occurrence, (string ComponentName, string JavaType, string TagKey) primary)
    {
        if (occurrence.EventSource is { } identity) return Identity(identity.Value, primary.JavaType);
        if (source?.TagKey != primary.TagKey || source.JavaType != primary.JavaType) return null;
        return CommandIdentity(primary.JavaType);
    }

    string? CommandIdentity(string javaType)
    {
        if (specification.When?.EventSource is { } destination) return Identity(destination.Value, javaType);
        if (source?.Input is not { } input) return null;
        var value = Input(input);
        return value is null ? null : Identity(value, javaType);
    }

    string? Identity(SemanticValue value, string javaType)
    {
        var type = CommandSource.TypeFor(context, javaType);
        var literal = _types.Literal(value, type);
        if (literal is null) return null;
        return type.Kind == SemanticTypeReferenceKind.Primitive ? $"new {javaType}({literal})" : literal;
    }
}
