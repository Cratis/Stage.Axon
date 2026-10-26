// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class AxonViewEvents
{
    internal static SemanticSpecificationEvent? Normalize(AxonEmissionContext context, SemanticSpecificationEvent given)
    {
        var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Id == given.EventContract);
        var primary = AxonEventSources.PrimaryTag(context, contract);
        var identity = contract.Properties.FirstOrDefault(property => JavaNames.Member(property.Name) == primary.ComponentName);
        if (given.EventSource?.Value is not SemanticTextValue) return null;

        // The flat projector reads $eventSourceId through the event's normalized primary tag, not an EventMessage.
        return identity is null ? given : given with { Values = [.. given.Values.Where(value => value.TargetProperty != identity.Id), new(identity.Id, given.EventSource.Value)] };
    }

    internal static string Expression(AxonEmissionContext context, ViewJavaTypes types, SemanticSpecificationEvent given, JavaCodeBuilder? code = null)
    {
        var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(candidate => candidate.Id == given.EventContract);
        var source = given.EventSource?.Value is SemanticTextValue text ? JavaCodeBuilder.StringLiteral(text.Value) : "\"spec-source\"";
        var primary = AxonEventSources.PrimaryTag(context, @event);
        var eventType = code is null ? types.EventType(given.EventContract) : types.EventType(given.EventContract, code);
        var arguments = @event.Properties.Select(property => given.Values.FirstOrDefault(value => value.TargetProperty == property.Id) is { } value
            ? Literal(types, code, value.Value, property.Type) : Default(types, code, property.Type)).ToList();
        if (!@event.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName))
        {
            var identity = $"new {(code is null ? primary.JavaType : code.Reference(primary.JavaType))}({source})";
            if (primary.JavaType != types.BasePackage + ".runtime.EventSourceId" && given.EventSource is { } eventSource)
                identity = Literal(types, code, eventSource.Value, eventSource.Type);
            arguments.Insert(0, identity);
        }
        return $"new {eventType}({string.Join(", ", arguments)})";
    }

    static string Literal(ViewJavaTypes types, JavaCodeBuilder? code, SemanticValue value, SemanticTypeReference type) =>
        code is null ? types.Literal(value, type) : types.Literal(value, type, code);

    static string Default(ViewJavaTypes types, JavaCodeBuilder? code, SemanticTypeReference type) =>
        code is null ? types.Default(type) : types.Default(type, code);
}
