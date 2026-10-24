// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter.given;

public class a_protected_decision_model : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    void Establish()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var command = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "MoveOrder");
        var view = slices.SelectMany(slice => slice.Slice.ReadModels).Single(model => model.Name == "Orders");
        var projection = slices.SelectMany(slice => slice.Slice.Projections).Single(projection => projection.ReadModel == view.Id);
        var removal = new SemanticProjectionRemoval(command.Produces[0].EventContract, new SemanticProjectionValueKey(SemanticProjectionValue.EventSourceIdentity), null);
        var reads = command with { Reads = [new("order", view.Id, command.Properties.Single(property => property.IsIdentifier).Id, SemanticDecisionReadPresence.Required)] };
        var app = loaded.Model.Application with
        {
            CommandReadMode = SemanticCommandReadMode.Protected,
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        var model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, app);
        request = new(model, loaded.Plan, request.Profile, new(ArtifactRenderScopeKind.Application, app.Id));

        SemanticFeature Feature(SemanticFeature feature) => feature with { Slices = [.. feature.Slices.Select(Slice)], Features = [.. feature.Features.Select(Feature)] };
        SemanticSlice Slice(SemanticSlice slice) => slice with
        {
            Commands = [.. slice.Commands.Select(candidate => candidate.Id == command.Id ? reads : candidate)],
            ReadModels = [.. slice.ReadModels.Select(candidate => candidate.Id == view.Id ? candidate with { Properties = [.. candidate.Properties.Select(property => property.Name == "orderId" ? property with { IsIdentifier = true } : property)] } : candidate)],
            Projections = [.. slice.Projections.Select(candidate => candidate.Id == projection.Id ? candidate with { Scope = candidate.Scope! with { Removals = [removal] } } : candidate)]
        };
    }
}
