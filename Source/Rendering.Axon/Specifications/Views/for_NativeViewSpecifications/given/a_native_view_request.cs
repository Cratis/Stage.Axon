// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Verification;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications.given;

public class a_native_view_request : Specification
{
    protected ArtifactRenderRequest _request;
    private protected AxonEmissionContext _context;
    private protected LocatedSemanticSlice _located;
    protected SemanticSpecification _spec;
    protected string _java;

    protected static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());

    protected static Task<ArtifactRenderRequest> Load(string path)
    {
        var root = for_AxonArtifactRenderPlanner.given.blueprint_models.Root();
        return LoadPath(Path.Combine(root, "Corpus", path));
    }

    protected static async Task<ArtifactRenderRequest> LoadCanonicalCorpus()
    {
        var root = for_AxonArtifactRenderPlanner.given.blueprint_models.Root();
        using var inputs = CanonicalCorpusInputs.Create(root);
        return await LoadPath(inputs.Root);
    }

    static async Task<ArtifactRenderRequest> LoadPath(string path)
    {
        var loaded = await SemanticModelLoader.LoadFromPathAsync(path, null, "NativeViews");
        return new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
    }

    async Task Establish()
    {
        _request = await Load("live-blueprints/models/catalog");
        _context = new(_request, new(_request.Profile));
        _located = ScopeSelection.Select(_request).Single(located => located.Slice.ReadModels.Any(model => model.Name == "Categories"));
        _spec = _located.Slice.Specifications.Single(spec => spec.Name == "ListingTheRegisteredCategory");
    }
}
