// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon.Emitters;

internal sealed class AxonEmissionContext(ArtifactRenderRequest request, AxonDegradation degradation)
{
    internal ArtifactRenderRequest Request { get; } = request;

    internal AxonDegradation Degradation { get; } = degradation;

    internal List<PlannedArtifact> Artifacts { get; } = [];
}
