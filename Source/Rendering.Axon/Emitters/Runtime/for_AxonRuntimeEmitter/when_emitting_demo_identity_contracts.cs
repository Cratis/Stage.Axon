// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_demo_identity_contracts : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_emit_positive_and_negative_controls_in_both_authentication_modes(bool bearer)
    {
        var configured = request with { Profile = bearer ? AxonRendering.WithBearerTokens(request.Profile) : request.Profile };
        var context = new AxonEmissionContext(configured, new(configured.Profile));
        new AxonRuntimeEmitter().Emit(context);
        var enabled = Source(context, "HttpDemoIdentityContractTest");
        var disabled = Source(context, "HttpDemoIdentityDisabledTest");
        enabled.ShouldContain("@ActiveProfiles(\"demo\")");
        enabled.ShouldContain("@PreAuthorize(\"hasRole('Sales')\")");
        enabled.ShouldContain("content().string(\"ROLE_Sales\")");
        enabled.ShouldContain("content().string(\"demo\")");
        enabled.ShouldContain("@PreAuthorize(\"denyAll()\")");
        enabled.ShouldContain("status().isForbidden()");
        enabled.ShouldContain("assertNull(getContext().getAuthentication())");
        disabled.ShouldNotContain("@ActiveProfiles");
        disabled.ShouldContain("status().isUnauthorized()");
    }

    static string Source(AxonEmissionContext context, string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.StartsWith("src/test/java/", StringComparison.Ordinal) && artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
