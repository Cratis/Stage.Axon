// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_inline_bodies_and_scopes : given.a_facade_model
{
    ArtifactRenderPlan _actual;
    ArtifactRenderPlan _expected;
    AxonRenderRequest _request;

    async Task Establish()
    {
        loaded = await SemanticModelLoader.LoadFromPathAsync(Path.Combine(blueprint_models.Root(), "Corpus", "axon-samples", "views"), null, "AxonViews");
        var application = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        var slices = ScopeSelection.Select(application).Take(2).ToArray();
        scope = new(ArtifactRenderScopeKind.Slice, slices[0].Slice.Id);
        _request = Request(new()) with { AdditionalScopes = [new(ArtifactRenderScopeKind.Slice, slices[1].Slice.Id)] };
        _expected = new AxonArtifactRenderPlanner().Plan(new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), scope)
        {
            AdditionalScopes = _request.AdditionalScopes,
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents,
            TypedContextDescriptors = loaded.TypedContextDescriptors,
            AttachmentDiagnostics = loaded.AttachmentDiagnostics,
            ReleaseBaseline = loaded.ReleaseBaseline
        });
    }

    void Because() => _actual = AxonRendering.Plan(_request);

    [Fact] void should_have_inline_bodies_to_preserve() => loaded.ImplementationContents.ShouldNotBeEmpty();
    [Fact] void should_keep_artifacts() => EqualArtifacts(_actual, _expected);
    [Fact] void should_keep_diagnostics() => EqualDiagnostics(_actual, _expected);
}
