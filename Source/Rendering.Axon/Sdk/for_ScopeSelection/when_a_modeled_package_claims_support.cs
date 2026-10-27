// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_ScopeSelection;

public class when_a_modeled_package_claims_support : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    ViewJavaTypes _types;

    void Establish() => _types = new(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name));

    [Fact]
    void should_refuse_each_reserved_package_in_both_modes()
    {
        foreach (var strict in new[] { false, true })
        {
            var request = _request with { Profile = AxonRendering.CreateProfile(strict: strict) };
            foreach (var package in _types.ReservedSupportPackages)
            {
                var diagnostic = _types.InspectPackage(package, ReadModelId("Total"), "Total");
                diagnostic.ShouldNotBeNull();
                diagnostic!.Code.ShouldEqual(AxonRefusalCodes.SupportPackage);
                diagnostic.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Error);
                diagnostic.Message.ShouldContain("Total");
                diagnostic.Message.ShouldContain(package);
                var plan = ArtifactRenderPlan.Create(request, [], [diagnostic], [], [], []);
                plan.Success.ShouldBeFalse();
                plan.Artifacts.ShouldBeEmpty();
            }
        }
    }

    [Fact]
    void should_identify_the_colliding_model_item() =>
        _types.InspectPackage(_types.BasePackage + ".persistence", ReadModelId("Total"), "Total")!.Artifact.ShouldEqual(ReadModelId("Total"));

    [Fact]
    void should_reserve_only_exact_packages()
    {
        foreach (var package in _types.ReservedSupportPackages)
        {
            _types.InspectPackage(package + ".models", ReadModelId("Total"), "Total").ShouldBeNull();
            _types.InspectPackage("other." + package, ReadModelId("Total"), "Total").ShouldBeNull();
        }
    }
}
