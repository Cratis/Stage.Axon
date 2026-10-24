// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Plans the experimental Axon target, preserving explicit diagnostics for unsupported modeled behavior.
/// </summary>
public sealed class AxonArtifactRenderPlanner : IArtifactRenderPlanner
{
    /// <inheritdoc/>
    /// <exception cref="InvalidArtifactRenderContract">The request does not satisfy the artifact render contract, including scope selection.</exception>
    public ArtifactRenderPlan Plan(ArtifactRenderRequest request)
    {
        var degradation = new AxonDegradation(request.Profile);
        var diagnostics = AxonAdmission.Inspect(request, degradation);
        var context = new AxonEmissionContext(request, degradation);
        if (diagnostics.IsEmpty && degradation.Refusals.IsEmpty)
        {
            Emit(context);
        }

        return ArtifactRenderPlan.Create(
            request,
            degradation.Refusals.IsEmpty ? [.. context.Artifacts] : [],
            [.. diagnostics, .. degradation.UnsupportedConstructs],
            [],
            degradation.Refusals,
            []);
    }

    static void Emit(AxonEmissionContext context)
    {
        foreach (var emitter in AxonEmitters.Ordered.Where(emitter => emitter.IsImplemented)) emitter.Emit(context);
        UnsupportedSummary.Emit(context);
        if (context.Artifacts.Count == 0)
        {
            context.Degradation.Unsupported(
                AxonRefusalCodes.Scaffold,
                context.Request.Model.Application.Id,
                "Axon scaffold emission is unavailable until the Phase 0 runtime has been verified.");
        }
    }
}
