// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionSpecificationValues(AxonEmissionContext context)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application);

    internal string? Trigger(SemanticApplicationTrigger trigger, SemanticSpecificationTrigger occurrence)
    {
        var values = trigger.Properties.Select(property => Property(property, occurrence.Values)).ToArray();
        if (values.Any(value => value is null)) return null;
        return $"new {context.BasePackage}.triggers.{JavaNames.Type(trigger.Name)}({string.Join(", ", values)})";
    }

    internal string? Event(SemanticSpecificationEvent occurrence)
    {
        if (occurrence.Route is not null || occurrence.Unrouted) return null;
        var contract = CommandAppends.Contract(context, occurrence.EventContract);
        var primary = AxonEventSources.PrimaryTag(context, contract);
        var values = contract.Properties.Select(property => EventProperty(property, occurrence, primary.ComponentName)).ToList();
        if (!contract.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName))
            values.Insert(0, Identity(occurrence.EventSource, primary.JavaType));
        if (values.Exists(value => value is null)) return null;
        return $"new {AxonEventsEmitter.TypeFor(context, contract.Id)}({string.Join(", ", values)})";
    }

    string? EventProperty(SemanticProperty property, SemanticSpecificationEvent occurrence, string primary)
    {
        if (JavaNames.Member(property.Name) != primary || occurrence.EventSource is not { } identity) return Property(property, occurrence.Values);
        var value = occurrence.Values.FirstOrDefault(value => value.TargetProperty == property.Id)?.Value;
        if (value is not null && value != identity.Value) return null;
        return _types.Literal(identity.Value, property.Type);
    }

    string? Identity(SemanticEventSourceIdentity? identity, string javaType)
    {
        if (identity is null) return null;
        var type = CommandSource.TypeFor(context, javaType);
        var value = _types.Literal(identity.Value, type);
        if (value is null) return null;
        return type.Kind == SemanticTypeReferenceKind.Primitive ? $"new {javaType}({value})" : value;
    }

    string? Property(SemanticProperty property, IEnumerable<SemanticPropertyValue> values)
    {
        var value = values.SingleOrDefault(value => value.TargetProperty == property.Id)?.Value;
        if (value is null) return property.Type.IsOptional ? "null" : null;
        return _types.Literal(value, property.Type);
    }
}
