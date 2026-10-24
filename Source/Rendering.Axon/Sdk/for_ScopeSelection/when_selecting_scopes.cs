// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_ScopeSelection;

public class when_selecting_scopes : an_axon_request
{
    [Fact]
    void should_select_every_slice_for_application_scope() =>
        ScopeSelection.Select(request).Count.ShouldEqual(ScopeSelection.Locate(loaded.Model.Application).Count());

    [Fact]
    void should_select_only_slices_in_a_module()
    {
        var module = loaded.Model.Application.Modules[0];
        var selection = ScopeSelection.Select(request with { Scope = new(ArtifactRenderScopeKind.Module, module.Id) });
        selection.ShouldNotBeEmpty();
        selection.All(slice => slice.Module.Id == module.Id).ShouldBeTrue();
    }

    [Fact]
    void should_select_only_slices_in_a_feature()
    {
        var feature = loaded.Model.Application.Modules[0].Features[0];
        var selection = ScopeSelection.Select(request with { Scope = new(ArtifactRenderScopeKind.Feature, feature.Id) });
        selection.ShouldNotBeEmpty();
        selection.All(slice => slice.Features.Any(ancestor => ancestor.Id == feature.Id)).ShouldBeTrue();
    }

    [Fact]
    void should_select_one_slice()
    {
        var slice = ScopeSelection.Locate(loaded.Model.Application).First();
        var selection = ScopeSelection.Select(request with { Scope = new(ArtifactRenderScopeKind.Slice, slice.Slice.Id) });
        selection.Count.ShouldEqual(1);
        selection[0].Slice.Id.ShouldEqual(slice.Slice.Id);
    }

    [Fact]
    void should_union_overlapping_scopes_without_duplicate_slices()
    {
        var slice = ScopeSelection.Locate(loaded.Model.Application).First();
        var moduleScope = new ArtifactRenderScope(ArtifactRenderScopeKind.Module, slice.Module.Id);
        var selection = ScopeSelection.Select(request with
        {
            Scope = moduleScope,
            AdditionalScopes = [new(ArtifactRenderScopeKind.Feature, slice.Features[0].Id), new(ArtifactRenderScopeKind.Slice, slice.Slice.Id), moduleScope]
        });
        selection.Select(item => item.Slice.Id).Distinct().Count().ShouldEqual(selection.Count);
        selection.Count.ShouldEqual(ScopeSelection.Select(request with { Scope = moduleScope }).Count);
    }

    [Fact]
    void should_keep_the_entire_nested_feature_path()
    {
        var module = loaded.Model.Application.Modules[0];
        var feature = module.Features[0];
        var child = feature with { Id = SemanticId.Parse($"sem1:{new string('a', 64)}"), Name = "Child", Features = [] };
        var application = loaded.Model.Application with { Modules = [module with { Features = [feature with { Slices = [], Features = [child] }] }] };
        var nested = ScopeSelection.Locate(application).First();
        nested.Features.Count.ShouldEqual(2);
        nested.Path.ShouldEqual(module.Name, feature.Name, child.Name, nested.Slice.Name);
    }
}
