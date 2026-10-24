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
    internal static string Expression(AxonEmissionContext context, ViewJavaTypes types, SemanticSpecificationEvent given)
    {
        var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(candidate => candidate.Id == given.EventContract);
        var source = given.EventSource?.Value is SemanticTextValue text ? JavaCodeBuilder.StringLiteral(text.Value) : "\"spec-source\"";
        var primary = AxonEventSources.PrimaryTag(context, @event);
        var arguments = @event.Properties.Select(property => given.Values.FirstOrDefault(value => value.TargetProperty == property.Id) is { } value ? types.Literal(value.Value, property.Type) : types.Default(property.Type)).ToList();
        if (!@event.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName))
        {
            var identity = $"new {primary.JavaType}({source})";
            if (primary.JavaType != types.BasePackage + ".runtime.EventSourceId" && given.EventSource is { } eventSource)
                identity = types.Literal(eventSource.Value, eventSource.Type);
            arguments.Insert(0, identity);
        }
        return $"new {types.EventType(given.EventContract)}({string.Join(", ", arguments)})";
    }
}
