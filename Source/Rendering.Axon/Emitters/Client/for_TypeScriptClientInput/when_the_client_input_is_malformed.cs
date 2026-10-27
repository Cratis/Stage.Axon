// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_TypeScriptClientInput;

public class when_the_client_input_is_malformed : Axon.for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan wrongVersion;
    ArtifactRenderPlan wrongBytes;
    ArtifactRenderPlan duplicated;

    static ArtifactRenderInput Input(string version, string bytes) => ArtifactRenderInput.Create(Admission.TypeScriptClientInput.Name, version, [.. Encoding.UTF8.GetBytes(bytes)]);

    ArtifactRenderPlan Plan(params ArtifactRenderInput[] inputs) =>
        AxonRendering.PlanFrom(loaded, request.Scope, profile: ArtifactRenderProfile.Create(request.Profile.Target, request.Profile.TargetVersion, request.Profile.Renderer, request.Profile.RendererVersion, [.. request.Profile.Inputs, .. inputs]));

    void Because()
    {
        wrongVersion = Plan(Input("2", "true"));
        wrongBytes = Plan(Input("1", "yes"));
        duplicated = Plan(Input("1", "true"), Input("2", "true"));
    }

    [Fact] void should_refuse_another_version() => wrongVersion.Diagnostics.Any(diagnostic => diagnostic.Code == Admission.AxonRefusalCodes.Profile).ShouldBeTrue();
    [Fact] void should_refuse_other_bytes() => wrongBytes.Diagnostics.Any(diagnostic => diagnostic.Code == Admission.AxonRefusalCodes.Profile).ShouldBeTrue();
    [Fact] void should_refuse_two_inputs() => duplicated.Diagnostics.Any(diagnostic => diagnostic.Code == Admission.AxonRefusalCodes.Profile).ShouldBeTrue();
    [Fact] void should_fail_every_plan() => new[] { wrongVersion, wrongBytes, duplicated }.All(plan => !plan.Success).ShouldBeTrue();
}
