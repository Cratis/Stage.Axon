// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_ScopeSelection;

public class when_checking_support_packages : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    readonly List<ArtifactRenderDiagnostic> _diagnostics = [];
    readonly List<string> _packages = [];

    void Because()
    {
        foreach (var name in new[] { "persistence", "runtime", "infrastructure", "", "---", "Per-sistence", "RUNTIME", "infra.structure" })
        {
            SemanticFeature Feature(SemanticFeature feature) => feature with
            {
                Name = name,
                Features = [.. feature.Features.Select(Feature)],
                Slices = [.. feature.Slices.Select(slice => slice with { Name = name })]
            };
            var application = _request.Model.Application;
            application = application with { Modules = [.. application.Modules.Select(module => module with { Name = name, Features = [.. module.Features.Select(Feature)] })] };
            var context = new AxonEmissionContext(_request, new(_request.Profile));
            var types = new ViewJavaTypes(application);
            foreach (var owner in ScopeSelection.Locate(application))
            {
                foreach (var package in new[] { types.Package(owner), AxonEventsEmitter.Package(context, owner.Module) })
                {
                    _packages.Add(package);
                    if (types.InspectPackage(package, owner.Slice.Id, owner.Slice.Name) is { } diagnostic) _diagnostics.Add(diagnostic);
                }
            }
        }
    }

    [Fact] void should_not_find_collisions_for_reserved_words_or_empty_segments() => _diagnostics.ShouldBeEmpty();

    [Fact]
    void should_never_derive_a_support_package()
    {
        var reserved = new ViewJavaTypes(_request.Model.Application).ReservedSupportPackages;
        _packages.ShouldNotBeEmpty();
        _packages.Exists(reserved.Contains).ShouldBeFalse();
    }

    [Fact]
    void should_keep_a_feature_level_even_in_flat_modules()
    {
        var application = _request.Model.Application;
        application = application with { Modules = [.. application.Modules.Select(module => module with { Features = [] })] };
        ScopeSelection.Locate(application).ShouldBeEmpty();
    }
}
