// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ProjectionHandlerShape
{
    internal static bool Scoped(SemanticProjection projection) => projection.Scope is { } scope &&
        (scope.Every is not null || new[] { scope.Children.Length, scope.Nested.Length, scope.Joins.Length, scope.Removals.Length, scope.JoinRemovals.Length }.Any(count => count > 0) ||
        scope.From.Any(from => from.Key is not SemanticProjectionValueKey || from.Mappings.Any(mapping => mapping.Target.Length > 1 || mapping.Source is SemanticProjectionEventContextValue)));

    internal static bool RootOnly(SemanticProjection projection) => projection.Scope is not { } scope ||
        (scope.Every is null && new[] { scope.Children.Length, scope.Nested.Length, scope.Joins.Length, scope.JoinRemovals.Length }.All(count => count == 0) &&
        scope.From.All(from => RootKey(from.Key)) && scope.Removals.All(removal => PartKey(removal.Key)));

    internal static bool RootKey(SemanticProjectionKey key) => key is SemanticProjectionValueKey || PartKey(key);

    internal static bool PartKey(SemanticProjectionKey key) => key is SemanticProjectionPartsKey { Parts.Length: > 0 };

    internal static string Arguments(SemanticProjection projection, ProjectionUpdates updates, string emitter = "null") =>
        (Scoped(projection) ? ", null" : string.Empty) + (updates.Enabled ? ", " + emitter : string.Empty);
}
