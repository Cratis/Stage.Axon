// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Events;

namespace Cratis.Stage.Rendering.Axon.Emitters.Decisions;

internal sealed class DecisionProjectionEvents(AxonEmissionContext context, CommandSource source)
{
    internal DecisionPresenceEvents? For(SemanticProjection projection)
    {
        if (projection.Target != SemanticProjectionTargetKind.ReadModel || projection.Scope is not { } scope) return null;
        if (!SingleLevel(scope)) return null;
        if (scope.From.IsEmpty || scope.From.Any(from => !Enrolled(from.EventContract, from.Key, from.ParentKey))) return null;
        if (scope.Removals.Any(removal => !Enrolled(removal.EventContract, removal.Key, removal.ParentKey))) return null;
        var creates = scope.From.Select(from => from.EventContract).Distinct().ToArray();
        var removes = scope.Removals.Select(removal => removal.EventContract).Distinct().ToArray();
        if (creates.Intersect(removes).Any()) return null;
        return new([.. creates], [.. removes]);
    }

    static bool SingleLevel(SemanticProjectionScope scope) =>
        scope.Joins.IsEmpty && scope.Children.IsEmpty && scope.Nested.IsEmpty && scope.Every is null && scope.JoinRemovals.IsEmpty;

    bool Enrolled(SemanticId id, SemanticProjectionKey key, SemanticProjectionKey? parent)
    {
        if (parent is not null || key is not SemanticProjectionValueKey { Value: SemanticProjectionEventSourceIdentity }) return false;
        var contract = CommandAppends.Contract(context, id);
        var tag = AxonEventSources.PrimaryTag(context, contract);
        return tag.JavaType == source.JavaType && tag.TagKey == source.TagKey;
    }
}
