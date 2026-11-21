// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;

namespace Cratis.Stage.Rendering.Axon.Sdk;

/// <summary>
/// Resolves which emitted operations sit under an availability gate (module, feature, slice or their own). Every operation source
/// (commands, declarative queries, synthesized performed queries and reactions) is enumerated here so emission and admission cannot disagree.
/// </summary>
internal static class AvailabilityGates
{
    /// <summary>Gets the ids of every operation in the slice that is under an availability gate.</summary>
    /// <param name="request">The render request.</param>
    /// <param name="located">The located slice.</param>
    /// <returns>The gated operation ids.</returns>
    internal static IEnumerable<SemanticId> Gated(ArtifactRenderRequest request, LocatedSemanticSlice located) =>
        Operations(request, located).Where(operation => located.HasAvailabilityGate || !operation.Available.IsDefaultOrEmpty).Select(operation => operation.Id);

    /// <summary>Gets the ids of the commands, declarative queries and reactions in the slice that are under an availability gate (no performed queries; for callers without a request).</summary>
    /// <param name="located">The located slice.</param>
    /// <returns>The gated operation ids.</returns>
    internal static IEnumerable<SemanticId> Gated(LocatedSemanticSlice located) =>
        DeclaredOperations(located).Where(operation => located.HasAvailabilityGate || !operation.Available.IsDefaultOrEmpty).Select(operation => operation.Id);

    /// <summary>Gets a value indicating whether the operation is under an availability gate.</summary>
    /// <param name="request">The render request.</param>
    /// <param name="owner">The operation id.</param>
    /// <returns>True when gated.</returns>
    internal static bool IsGated(ArtifactRenderRequest request, SemanticId owner) =>
        ScopeSelection.Select(request).Any(located => Gated(request, located).Contains(owner));

    static IEnumerable<(SemanticId Id, ImmutableArray<SemanticAvailabilityRequirement> Available)> DeclaredOperations(LocatedSemanticSlice located) =>
        located.Slice.Commands.Select(command => (command.Id, command.Available))
            .Concat(located.Slice.Queries.Select(query => (query.Id, query.Available)))

            // Reactions inherit the scope gates and have none of their own, whether they invoke commands or produce events.
            .Concat(located.Slice.Reactions.Select(reaction => (reaction.Id, default(ImmutableArray<SemanticAvailabilityRequirement>))));

    static IEnumerable<(SemanticId Id, ImmutableArray<SemanticAvailabilityRequirement> Available)> Operations(ArtifactRenderRequest request, LocatedSemanticSlice located) =>
        DeclaredOperations(located)
            .Concat(QueryPerformer.Queries(request, located).Select(query => (query.Id, query.Available)));
}
