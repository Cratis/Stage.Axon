// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ProjectionHandlerShape
{
    internal static bool Scoped(SemanticProjection projection) => projection.Scope is { } scope &&
        (scope.Every is not null || new[] { scope.Children.Length, scope.Nested.Length, scope.Joins.Length, scope.JoinRemovals.Length, scope.Removals.Count(removal => removal.ParentKey is not null) }.Any(count => count > 0) ||
        scope.From.Any(from => from.Key is not SemanticProjectionValueKey || from.Mappings.Any(mapping => mapping.Target.Length > 1 || mapping.Source is SemanticProjectionEventContextValue)));

    internal static bool RootOnly(SemanticProjection projection) => projection.Scope is not { } scope ||
        (scope.Every is null && new[] { scope.Children.Length, scope.Nested.Length, scope.Joins.Length, scope.JoinRemovals.Length, scope.Removals.Count(removal => removal.ParentKey is not null) }.All(count => count == 0) &&
        scope.From.All(from => from.Key is SemanticProjectionValueKey));

    internal static bool RootOnly(SemanticApplication application, SemanticProjection projection, SemanticReadModel model) => RootOnly(projection) ||
        (ReadModelKeys.IsComposite(application, model) && projection.Scope is { } scope && scope.Every is null &&
        new[] { scope.Children.Length, scope.Nested.Length, scope.Joins.Length, scope.JoinRemovals.Length, scope.Removals.Count(removal => removal.ParentKey is not null) }.All(count => count == 0) &&
        scope.From.All(from => from.Key is SemanticProjectionValueKey or SemanticProjectionPartsKey or SemanticProjectionCompositeKey) &&
        scope.Removals.All(removal => removal.Key is SemanticProjectionValueKey or SemanticProjectionPartsKey or SemanticProjectionCompositeKey));

    internal static string Arguments(SemanticProjection projection, ProjectionUpdates updates, string emitter = "null") =>
        (Scoped(projection) ? ", null" : string.Empty) + (updates.Enabled ? ", " + emitter : string.Empty);
}
