// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Sdk;

internal static class ScopeSelection
{
    internal static IReadOnlyList<LocatedSemanticSlice> Select(ArtifactRenderRequest request) =>
        [.. Locate(request.Model.Application).Where(slice => new[] { request.Scope }.Concat(request.AdditionalScopes).Any(scope => Matches(scope, slice)))];

    internal static bool Exists(ArtifactRenderRequest request, ArtifactRenderScope scope) => scope.Kind switch
    {
        ArtifactRenderScopeKind.Application => scope.Artifact == request.Model.Application.Id,
        ArtifactRenderScopeKind.Module => ModuleExists(request.Model.Application, scope),
        ArtifactRenderScopeKind.Feature => FeatureExists(request.Model.Application, scope),
        ArtifactRenderScopeKind.Slice => SliceExists(request.Model.Application, scope),
        _ => false
    };

    internal static IEnumerable<LocatedSemanticSlice> Locate(SemanticApplication application) =>
        application.Modules.SelectMany(module => module.Features.SelectMany(feature => Locate(module, feature, [], [module.Name])));

    static bool ModuleExists(SemanticApplication application, ArtifactRenderScope scope) =>
        application.Modules.Any(module => module.Id == scope.Artifact);

    static bool FeatureExists(SemanticApplication application, ArtifactRenderScope scope) =>
        application.Modules.SelectMany(module => module.Features).SelectMany(Features).Any(feature => feature.Id == scope.Artifact);

    static bool SliceExists(SemanticApplication application, ArtifactRenderScope scope) =>
        Locate(application).Any(slice => slice.Slice.Id == scope.Artifact);

    static IEnumerable<SemanticFeature> Features(SemanticFeature feature) =>
        new[] { feature }.Concat(feature.Features.SelectMany(Features));

    static bool Matches(ArtifactRenderScope scope, LocatedSemanticSlice slice) => scope.Kind switch
    {
        ArtifactRenderScopeKind.Application => true,
        ArtifactRenderScopeKind.Module => slice.Module.Id == scope.Artifact,
        ArtifactRenderScopeKind.Feature => slice.Features.Any(feature => feature.Id == scope.Artifact),
        ArtifactRenderScopeKind.Slice => slice.Slice.Id == scope.Artifact,
        _ => false
    };

    static IEnumerable<LocatedSemanticSlice> Locate(SemanticModule module, SemanticFeature feature, IReadOnlyList<SemanticFeature> ancestors, IReadOnlyList<string> path)
    {
        SemanticFeature[] features = [.. ancestors, feature];
        string[] featurePath = [.. path, feature.Name];
        return feature.Slices.Select(slice => new LocatedSemanticSlice(module, features, slice, [.. featurePath, slice.Name]))
            .Concat(feature.Features.SelectMany(child => Locate(module, child, features, featurePath)));
    }
}
