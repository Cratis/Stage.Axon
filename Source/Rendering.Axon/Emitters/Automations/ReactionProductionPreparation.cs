// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionProductionPreparation(AxonEmissionContext context, SemanticProducedEvent produced, ReactionInput input)
{
    internal ReactionProduction? Prepare(string name)
    {
        if (!Supported()) return null;
        var contract = CommandAppends.Contract(context, produced.EventContract);
        var primary = AxonEventSources.PrimaryTag(context, contract);
        if (!MatchesPrimary(primary.JavaType)) return null;
        var source = SourceValue(primary.JavaType);
        if (source is null) return null;
        var arguments = Arguments(contract, primary.ComponentName, source);
        if (arguments is null) return null;
        var condition = produced.When is null ? null : input.Condition(context, produced.When);
        if (produced.When is not null && condition is null) return null;
        var eventType = AxonEventsEmitter.TypeFor(context, contract.Id);
        return new(name, new(eventType, $"new {eventType}({string.Join(", ", arguments)})", source, condition), primary);
    }

    bool Supported()
    {
        if (produced.Route is not null || !produced.Tags.IsEmpty) return false;
        if (produced.Condition is not null && produced.When is null) return false;
        return !Constrained();
    }

    bool Constrained() => ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Constraints)
        .Any(constraint => constraint.Targets.Any(target => target.EventContract == produced.EventContract) || constraint.ReleasedBy.Contains(produced.EventContract));

    bool MatchesPrimary(string javaType) => produced.DestinationType is not { } destination || destination.Equals(CommandSource.TypeFor(context, javaType));

    List<string?>? Arguments(SemanticEventContract contract, string member, string source)
    {
        var arguments = contract.Properties.Select(property => Argument(property, member, source)).ToList();
        if (arguments.Exists(argument => argument is null)) return null;
        if (!contract.Properties.Any(property => JavaNames.Member(property.Name) == member)) arguments.Insert(0, source);
        return arguments;
    }

    string? SourceValue(string javaType)
    {
        var type = CommandSource.TypeFor(context, javaType);
        var expression = produced.Destination ?? new SemanticEventContextExpression(SemanticEventContextValueKind.EventSourceIdentity, type);
        var value = input.Value(context, expression, type);
        if (value is null) return null;
        return type.Kind == SemanticTypeReferenceKind.Primitive ? $"new {javaType}({value})" : value;
    }

    string? Argument(SemanticProperty property, string member, string source)
    {
        var mapping = produced.Mappings.SingleOrDefault(mapping => mapping.TargetProperty == property.Id);
        if (JavaNames.Member(property.Name) == member)
        {
            if (mapping is not null && !MatchesDestination(mapping.Source)) return null;
            return source;
        }
        if (mapping is null) return property.Type.IsOptional ? "null" : null;
        return input.Value(context, mapping.Source, property.Type);
    }

    bool MatchesDestination(SemanticExpression expression) =>
        produced.Destination is { } destination ? destination.Equals(expression) : expression is SemanticEventContextExpression { Value: SemanticEventContextValueKind.EventSourceIdentity };
}
