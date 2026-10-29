// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_PersistentProfile.given;

/// <summary>The orders request with one unique-property-value constraint on <c>OrderPlaced</c>.</summary>
public class a_constrained_orders_request : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    protected ArtifactRenderRequest constrained;

    void Establish()
    {
        var located = ScopeSelection.Select(request).Single(slice => slice.Slice.Events.Any(contract => contract.Name == "OrderPlaced"));
        var target = located.Slice.Events.Single(contract => contract.Name == "OrderPlaced");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(target.Id, [target.Properties.Single(property => property.Name == "customer").Id])], [], false, null);
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice.Id == located.Slice.Id ? slice with { Constraints = [constraint] } : slice)]
        };
        var application = request.Model.Application;
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        constrained = request with { Model = model };
    }
}
