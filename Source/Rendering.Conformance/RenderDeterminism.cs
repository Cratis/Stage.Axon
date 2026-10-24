// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Checks repeated rendering without substituting hashes for artifact comparison.</summary>
public static class RenderDeterminism
{
    /// <summary>Renders twice and compares artifact paths, kinds, bytes, sources and typed plan metadata.</summary>
    /// <param name="planner">The renderer under test.</param>
    /// <param name="request">The same immutable request for both renders.</param>
    /// <returns>A violation when the two complete plans differ.</returns>
    /// <remarks>Give the planner a fixed clock or generation seed wherever the request permits one. Differences caused by time or randomness are real violations, not ignored noise.</remarks>
    public static CoverageReport Check(IArtifactRenderPlanner planner, ArtifactRenderRequest request)
    {
        var first = planner.Plan(request);
        var second = planner.Plan(request);
        return Equivalent(first, second) ? new([]) : new([new(ConformanceCodes.NondeterministicOutput, "Repeated rendering produced different artifacts or typed plan metadata.")]);
    }

    static bool Equivalent(ArtifactRenderPlan first, ArtifactRenderPlan second) =>
        ArtifactsEqual(first, second) && JsonSerializer.Serialize(Metadata(first)) == JsonSerializer.Serialize(Metadata(second));

    static object Metadata(ArtifactRenderPlan plan) => new
    {
        plan.Target,
        plan.TargetVersion,
        plan.Renderer,
        plan.RendererVersion,
        plan.ApplicationName,
        plan.SemanticRevision,
        plan.Scope,
        plan.AdditionalScopes,
        plan.Diagnostics,
        plan.Refusals,
        plan.ImplementationGaps,
        plan.RequiredRemovals
    };

    static bool ArtifactsEqual(ArtifactRenderPlan first, ArtifactRenderPlan second) =>
        first.Artifacts.Length == second.Artifacts.Length && first.Artifacts.Zip(second.Artifacts).All(pair =>
            pair.First.RelativePath == pair.Second.RelativePath && pair.First.Kind == pair.Second.Kind &&
            pair.First.Bytes.AsSpan().SequenceEqual(pair.Second.Bytes.AsSpan()) && pair.First.Sources.SequenceEqual(pair.Second.Sources));
}
