// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

/// <summary>
/// One join of a scoped projection: the node that owns it, the join and the ordinal of its column in the projection-owned lookup table.
/// </summary>
/// <param name="Ordinal">The ordinal of the join in the projection.</param>
/// <param name="Node">The scope node that declares the join.</param>
/// <param name="Index">The index of the join in the node's scope.</param>
/// <param name="Join">The modeled join.</param>
internal sealed record ProjectionJoinSlot(int Ordinal, ProjectionScopeNode Node, int Index, SemanticProjectionJoin Join)
{
    /// <summary>Gets the lookup row key prefix of the join.</summary>
    internal string Prefix => "join" + Ordinal + "|";

    /// <summary>Gets the lookup column and accessor name of the join.</summary>
    internal string Member => "join" + Ordinal;

    internal static IReadOnlyList<ProjectionJoinSlot> All(ProjectionScopeNode root)
    {
        var slots = new List<ProjectionJoinSlot>();
        foreach (var node in root.All)
        {
            for (var index = 0; index < node.Scope.Joins.Length; index++) slots.Add(new(slots.Count, node, index, node.Scope.Joins[index]));
        }
        return slots;
    }
}
