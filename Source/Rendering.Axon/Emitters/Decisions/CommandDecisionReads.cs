// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Decisions;

internal sealed class CommandDecisionReads(AxonEmissionContext context, SemanticCommand command, CommandSource? source)
{
    internal ImmutableArray<PreparedDecisionRead>? Prepare()
    {
        if (command.Reads.IsEmpty) return [];
        if (source?.Generated != false || command.Route is not null) return null;
        if (command.Produces.Any(produced => produced.Route is not null)) return null;
        var reads = command.Reads.Select(PrepareRead).ToArray();
        return reads.Any(read => read is null) ? null : [.. reads.OfType<PreparedDecisionRead>()];
    }

    internal void Check(JavaCodeBuilder body)
    {
        foreach (var read in Prepare() ?? throw new InvalidOperationException("Cannot emit unsupported decision reads."))
        {
            var message = JavaCodeBuilder.StringLiteral($"Required decision read '{read.Name}' was not found.");
            body.Line($"if (stream.entity() == null || !stream.entity().{read.Member}()) throw new {context.BasePackage}.runtime.CommandValidationException({message});");
        }
    }

    PreparedDecisionRead? PrepareRead(SemanticDecisionRead read, int index)
    {
        if (read.Presence != SemanticDecisionReadPresence.Required || source!.Input?.Id != read.Key) return null;
        var projections = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Projections)
            .Where(projection => projection.ReadModel == read.ReadModel).ToArray();
        if (projections.Length != 1) return null;
        var events = new DecisionProjectionEvents(context, source!).For(projections[0]);
        return events is null ? null : new("decisionRead" + index, read.Name, events);
    }
}
