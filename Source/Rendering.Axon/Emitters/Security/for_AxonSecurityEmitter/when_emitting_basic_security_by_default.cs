// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_AxonSecurityEmitter;

public class when_emitting_basic_security_by_default : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonSecurityEmitter().Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_keep_basic() => code.ShouldContain(".httpBasic(Customizer.withDefaults()).build();");
    [Theory]
    [InlineData("oauth2")]
    [InlineData("Jwt")]
    [InlineData("BearerToken")]
    void should_not_include_bearer_configuration(string text) => code.ShouldNotContain(text);
}
