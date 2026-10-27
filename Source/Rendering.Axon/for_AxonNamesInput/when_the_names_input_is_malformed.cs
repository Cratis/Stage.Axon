// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonNamesInput;

public class when_the_names_input_is_malformed : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan _wrongVersion;
    ArtifactRenderPlan _wrongBytes;
    ArtifactRenderPlan _nonCanonical;
    ArtifactRenderPlan _duplicated;

    static ArtifactRenderInput Input(string version, string bytes) => ArtifactRenderInput.Create(AxonNamesInput.Name, version, [.. Encoding.UTF8.GetBytes(bytes)]);

    ArtifactRenderPlan Plan(params ArtifactRenderInput[] inputs) => AxonRendering.PlanFrom(
        loaded,
        request.Scope,
        profile: ArtifactRenderProfile.Create(request.Profile.Target, request.Profile.TargetVersion, request.Profile.Renderer, request.Profile.RendererVersion, [.. request.Profile.Inputs, .. inputs]));

    void Because()
    {
        _wrongVersion = Plan(Input("2", "{\"javaPackage\":\"acme\"}"));
        _wrongBytes = Plan(Input("1", "not JSON"));
        _nonCanonical = Plan(Input("1", "{ \"javaPackage\": \"acme\" }"));
        _duplicated = Plan(AxonNamesInput.Create("acme", null), Input("2", "{\"javaPackage\":\"acme\"}"));
    }

    [Fact] void should_refuse_another_version() => _wrongVersion.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-PROFILE").ShouldBeTrue();
    [Fact] void should_refuse_bad_bytes() => _wrongBytes.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-PROFILE").ShouldBeTrue();
    [Fact] void should_refuse_non_canonical_json() => _nonCanonical.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-PROFILE").ShouldBeTrue();
    [Fact] void should_refuse_duplicate_inputs() => _duplicated.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-PROFILE").ShouldBeTrue();
    [Fact] void should_fail_every_plan() => new[] { _wrongVersion, _wrongBytes, _nonCanonical, _duplicated }.All(plan => !plan.Success).ShouldBeTrue();
}
