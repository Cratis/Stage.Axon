// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_http_contract_test : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_leave_framework_security_tests_to_spring(bool bearer)
    {
        var configured = request with { Profile = bearer ? AxonRendering.WithBearerTokens(request.Profile) : request.Profile };
        var context = new AxonEmissionContext(configured, new(configured.Profile));
        new AxonRuntimeEmitter().Emit(context);
        foreach (var artifact in context.Artifacts)
        {
            artifact.RelativePath.Contains("/Http", StringComparison.Ordinal).ShouldBeFalse();
            artifact.RelativePath.Contains("/RuntimeHttp", StringComparison.Ordinal).ShouldBeFalse();
        }
    }
}
