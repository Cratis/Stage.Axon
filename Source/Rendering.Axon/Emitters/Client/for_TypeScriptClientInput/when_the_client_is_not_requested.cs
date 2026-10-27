// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_TypeScriptClientInput;

public class when_the_client_is_not_requested : Axon.for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan plan;

    void Because() => plan = AxonRendering.PlanFrom(loaded, request.Scope);

    [Fact] void should_not_carry_the_input() => request.Profile.Inputs.Any(input => input.Name == Admission.TypeScriptClientInput.Name).ShouldBeFalse();
    [Fact] void should_render() => plan.Success.ShouldBeTrue();
    [Fact] void should_not_render_client_paths() => plan.Artifacts.Any(artifact => artifact.RelativePath.StartsWith("client/", StringComparison.Ordinal)).ShouldBeFalse();
}
