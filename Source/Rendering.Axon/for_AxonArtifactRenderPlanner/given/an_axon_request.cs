// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;

public class an_axon_request : Specification
{
    protected LoadedSemanticModel loaded;
    protected ArtifactRenderRequest request;

    async Task Establish()
    {
        loaded = await blueprint_models.Load("orders");
        request = new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
    }
}
