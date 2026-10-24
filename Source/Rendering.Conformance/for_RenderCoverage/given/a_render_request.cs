// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Conformance.for_RenderCoverage.given;

public class a_render_request : Specification
{
    protected ArtifactRenderRequest request;

    public static string Root([CallerFilePath] string path = "")
    {
        var directory = Directory.GetParent(path);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Stage.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    protected ArtifactRenderPlan Plan(ImmutableArray<PlannedArtifact> artifacts, ImmutableArray<ArtifactRenderDiagnostic> diagnostics) =>
        ArtifactRenderPlan.Create(request, artifacts, diagnostics);

    protected SemanticId[] Identities() => [.. SemanticIdentities.Definitions(request.Model).Keys];

    async Task Establish()
    {
        var loaded = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(Root(), "Verification", "live-blueprints", "models", "orders"), null, "Sample");
        request = new(loaded.Model, loaded.Plan, ArtifactRenderProfile.Create("test", "1", "test", "1", []), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
    }
}
