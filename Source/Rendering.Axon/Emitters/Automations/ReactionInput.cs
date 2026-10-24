// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionInput(string javaType, ImmutableArray<SemanticProperty> properties, SemanticEventContract? @event)
{
    internal string JavaType => javaType;
    internal SemanticEventContract? Event => @event;

    internal static ReactionInput? Create(AxonEmissionContext context, SemanticReactionTrigger trigger)
    {
        if (trigger.Kind == SemanticReactionTriggerKind.Event)
        {
            var contract = CommandAppends.Contract(context, trigger.Source);
            return new(AxonEventsEmitter.TypeFor(context, contract.Id), contract.Properties, contract);
        }
        if (trigger.Kind != SemanticReactionTriggerKind.ApplicationTrigger) return new(string.Empty, [], null);
        var applicationTrigger = context.Request.Model.Application.Triggers.SingleOrDefault(candidate => candidate.Id == trigger.Source);
        return applicationTrigger is null ? null : new(context.BasePackage + ".triggers." + JavaNames.Type(applicationTrigger.Name), applicationTrigger.Properties, null);
    }

    internal string? Value(AxonEmissionContext context, SemanticExpression expression, SemanticTypeReference target)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application);
        var conversion = new CommandExpressions(Command(), types);
        return expression switch
        {
            SemanticValueExpression literal => types.Literal(literal.Value, target),
            SemanticResolvedExpression reference when IsProperty(reference) => Property(reference, target, conversion),
            SemanticEventContextExpression occurrence => EventContext(context, occurrence, target, conversion),
            _ => null
        };
    }

    internal string? Condition(AxonEmissionContext context, SemanticCondition condition) =>
        new CommandConditions(Command(), new(context.Request.Model.Application)).Render(condition)?.Replace("command.", "input.", StringComparison.Ordinal);

    bool IsProperty(SemanticResolvedExpression reference) => reference.Source == SemanticExpressionSourceKind.Property &&
        reference.Root == (Event is null ? SemanticExpressionRootKind.Trigger : SemanticExpressionRootKind.Event);

    string? Property(SemanticResolvedExpression reference, SemanticTypeReference target, CommandExpressions conversion)
    {
        var property = properties.SingleOrDefault(property => property.Id == reference.Target);
        return property is null ? null : conversion.Convert("input." + JavaNames.Member(property.Name) + "()", property.Type, target);
    }

    string? EventContext(AxonEmissionContext context, SemanticEventContextExpression occurrence, SemanticTypeReference target, CommandExpressions conversion)
    {
        if (occurrence.Value == SemanticEventContextValueKind.Occurred)
            return conversion.Convert("java.time.OffsetDateTime.ofInstant(" + (Event is null ? "clock.instant()" : "occurred") + ", java.time.ZoneOffset.UTC)", SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime), target);
        if (occurrence.Value != SemanticEventContextValueKind.EventSourceIdentity || Event is null) return null;
        var primary = AxonEventSources.PrimaryTag(context, Event);
        var type = CommandSource.TypeFor(context, primary.JavaType);
        var value = "input." + primary.ComponentName + "()" + (type.Kind == SemanticTypeReferenceKind.Primitive ? ".value()" : string.Empty);
        return conversion.Convert(value, type, target);
    }

    SemanticCommand Command() => new(default, "ReactionInput", properties, [], []);
}
