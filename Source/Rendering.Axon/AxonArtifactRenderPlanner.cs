// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Admits requests for the experimental Axon target. Scaffold emission awaits the verified Phase 0 runtime.
/// No domain behavior or executable application is claimed by this initial skeleton.
/// </summary>
public sealed class AxonArtifactRenderPlanner : IArtifactRenderPlanner
{
    /// <inheritdoc/>
    /// <exception cref="InvalidArtifactRenderContract">The request does not satisfy the artifact render contract, including scope selection.</exception>
    public ArtifactRenderPlan Plan(ArtifactRenderRequest request)
    {
        var diagnostics = AxonAdmission.Inspect(request);
        if (diagnostics.IsEmpty)
        {
            diagnostics = [new(
                AxonRefusalCodes.Scaffold,
                ArtifactRenderDiagnosticSeverity.Error,
                "Axon scaffold emission is unavailable until the Phase 0 runtime has been verified.",
                request.Model.Application.Id)];
        }

        return ArtifactRenderPlan.Create(request, [], diagnostics);
    }
}
