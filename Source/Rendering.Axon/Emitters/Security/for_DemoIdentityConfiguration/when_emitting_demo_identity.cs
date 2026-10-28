// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_DemoIdentityConfiguration;

public class when_emitting_demo_identity : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_leave_local_identities_to_native_spring_configuration(bool bearer)
    {
        var configured = request with { Profile = bearer ? AxonRendering.WithBearerTokens(request.Profile) : request.Profile };
        var context = new AxonEmissionContext(configured, new(configured.Profile));
        new AxonSecurityEmitter().Emit(context);
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/DemoIdentityConfiguration.java", StringComparison.Ordinal)).ShouldBeFalse();
        var security = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
        security.ShouldNotContain("@Profile");
        security.ShouldContain("@EnableMethodSecurity");
        security.ShouldNotContain("SecurityContextHolder");
    }
}
