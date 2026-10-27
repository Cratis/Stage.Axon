// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_HttpAuthenticationInput;

public class when_the_authentication_input_is_malformed : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData("2", "bearer", false)]
    [InlineData("1", "basic", false)]
    [InlineData("1", "jwt", false)]
    [InlineData("1", "bearer", true)]
    void should_refuse_the_profile(string version, string bytes, bool duplicate)
    {
        var input = ArtifactRenderInput.Create(HttpAuthenticationInput.Name, version, [.. Encoding.UTF8.GetBytes(bytes)]);
        var otherVersion = ArtifactRenderInput.Create(HttpAuthenticationInput.Name, "2", [.. Encoding.UTF8.GetBytes("bearer")]);
        var profile = ArtifactRenderProfile.Create(request.Profile.Target, request.Profile.TargetVersion, request.Profile.Renderer, request.Profile.RendererVersion, duplicate ? [.. request.Profile.Inputs, input, otherVersion] : [.. request.Profile.Inputs, input]);
        var plan = AxonRendering.PlanFrom(loaded, request.Scope, profile: profile);
        plan.Diagnostics.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.Profile).ShouldBeTrue();
        plan.Success.ShouldBeFalse();
        HttpAuthenticationInput.Bearer(profile).ShouldBeFalse();
    }
}
