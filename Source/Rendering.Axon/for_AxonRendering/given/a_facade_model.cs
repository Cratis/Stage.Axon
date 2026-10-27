// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.given;

public class a_facade_model : Specification
{
    protected LoadedSemanticModel loaded;
    protected ArtifactRenderScope scope;

    async Task Establish()
    {
        loaded = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(blueprint_models.Root(), "Corpus", "live-blueprints", "models", "orders"), null, "Facadeproof");
        scope = new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id);
    }

    protected AxonRenderRequest Request(AxonRenderOptions options) => new(loaded.Model, loaded.Plan, scope)
    {
        ImplementationRequirements = loaded.ImplementationRequirements,
        ImplementationContents = loaded.ImplementationContents,
        TypedContextDescriptors = loaded.TypedContextDescriptors,
        AttachmentDiagnostics = loaded.AttachmentDiagnostics,
        ReleaseBaseline = loaded.ReleaseBaseline,
        Options = options
    };

    protected static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());

    protected static void EqualArtifacts(ArtifactRenderPlan actual, ArtifactRenderPlan expected) =>
        actual.Artifacts.Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan())))
            .ShouldEqual(expected.Artifacts.Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan()))));

    protected static void EqualDiagnostics(ArtifactRenderPlan actual, ArtifactRenderPlan expected) =>
        actual.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Severity))
            .ShouldEqual(expected.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Severity)));
}
