// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandAppends(AxonEmissionContext context, SemanticCommand command, CommandSource source)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application);

    internal bool UsesOccurred { get; private set; }

    internal static SemanticEventContract Contract(AxonEmissionContext context, SemanticId id) => ScopeSelection.Locate(context.Request.Model.Application)
        .SelectMany(slice => slice.Slice.Events).DistinctBy(contract => contract.Id).Single(contract => contract.Id == id);

    internal (string Event, string? Condition)? Build(SemanticProducedEvent produced)
    {
        var contract = Contract(context, produced.EventContract);
        var primary = AxonEventSources.PrimaryTag(context, contract);
        if (!CanAppend(produced, primary)) return null;
        var arguments = contract.Properties.Select(property => Argument(produced, property)).ToList();
        if (!contract.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName)) arguments.Insert(0, "command." + source.Member + "()");
        if (arguments.Exists(argument => argument is null)) return null;
        var condition = produced.When is { } when ? new CommandConditions(command, _types).Render(when) : null;
        if (produced.When is not null && condition is null) return null;
        return ($"new {AxonEventsEmitter.TypeFor(context, contract.Id)}({string.Join(", ", arguments)})", condition);
    }

    string? Argument(SemanticProducedEvent produced, SemanticProperty property)
    {
        var mapping = produced.Mappings.FirstOrDefault(mapping => mapping.TargetProperty == property.Id);
        if (JavaNames.Member(property.Name) == source.Member)
        {
            return PrimaryArgument(property, mapping?.Source);
        }
        if (mapping is null) return property.Type.IsOptional ? "null" : null;
        return mapping.Source is SemanticEventContextExpression occurrence ? Occurrence(occurrence, property.Type)
            : new CommandExpressions(command, _types).Render(mapping.Source, property.Type);
    }

    bool CanAppend(SemanticProducedEvent produced, (string ComponentName, string JavaType, string TagKey) primary)
    {
        if (produced.Condition is not null && produced.When is null) return false;
        if (primary.TagKey != source.TagKey || primary.JavaType != source.JavaType) return false;
        return (produced.Destination ?? command.Destination?.Value) == source.Destination;
    }

    string? PrimaryArgument(SemanticProperty property, SemanticExpression? mapping)
    {
        if (_types.Type(property.Type) != source.JavaType) return null;
        if (mapping is SemanticResolvedExpression reference && reference.Target != source.Input?.Id) return null;
        if (mapping is SemanticValueExpression literal && literal != source.Destination) return null;
        return "command." + source.Member + "()";
    }

    string? Occurrence(SemanticEventContextExpression expression, SemanticTypeReference target)
    {
        var expressions = new CommandExpressions(command, _types);
        if (expression.Value == SemanticEventContextValueKind.Occurred) UsesOccurred = true;
        return expression.Value switch
        {
            SemanticEventContextValueKind.EventSourceIdentity => expressions.Convert("command." + source.Member + "()" + (source.RuntimeId ? ".value()" : string.Empty), source.Type, target),
            SemanticEventContextValueKind.Occurred => expressions.Convert("occurred", SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DateTime), target),
            SemanticEventContextValueKind.CausedBySubject => expressions.Convert(context.BasePackage + ".runtime.Caller.current().identity()", SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text), target),
            _ => null
        };
    }
}
