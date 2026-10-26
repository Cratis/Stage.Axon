// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Samples.MinimalTarget;

internal sealed class ApplicationManifestPlanner : IArtifactRenderPlanner
{
    public ArtifactRenderPlan Plan(ArtifactRenderRequest request)
    {
        var hasExpectedScope = request.Scope == new ArtifactRenderScope(ArtifactRenderScopeKind.Application, request.Model.Application.Id);
        var hasNoAdditionalScopes = request.AdditionalScopes is { IsDefault: false, IsEmpty: true };
        if (!hasExpectedScope || !hasNoAdditionalScopes)
        {
            throw new InvalidArtifactRenderContract("The manifest sample requires the model's application scope and initialized, empty additional scopes.");
        }

        var artifact = PlannedArtifact.CreateText("application.md", $"# {request.Model.Application.Name}\n", [request.Model.Application.Id]);
        return ArtifactRenderPlan.Create(request, [artifact], []);
    }
}
