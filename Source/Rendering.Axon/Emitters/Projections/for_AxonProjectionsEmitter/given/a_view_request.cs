// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;

public class a_view_request : Specification
{
    protected ArtifactRenderRequest _request;

    protected SemanticId ReadModelId(string name) => ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.ReadModels).Single(model => model.Name == name).Id;

    protected SemanticId QueryId(string name) => ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.Queries).Single(query => query.Name == name).Id;

    protected static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());

    static string Root([CallerFilePath] string path = "")
    {
        var directory = Directory.GetParent(path);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Stage.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    async Task Establish()
    {
        var model = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(Root(), "Verification", "axon-samples", "views"), null, "AxonViews");
        _request = new(model.Model, model.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, model.Model.Application.Id))
        {
            ImplementationRequirements = model.ImplementationRequirements,
            ImplementationContents = model.ImplementationContents
        };
    }
}
