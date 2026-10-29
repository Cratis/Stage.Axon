// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_http_contract_test : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_emit_native_http_contract_tests_for_the_selected_profile(bool bearer)
    {
        var configured = request with { Profile = bearer ? AxonRendering.WithBearerTokens(request.Profile) : request.Profile };
        var context = new AxonEmissionContext(configured, new(configured.Profile));
        new AxonRuntimeEmitter().Emit(context);
        foreach (var name in new[] { "HttpErrorsContractTest", "HttpPublicContractTest", bearer ? "HttpBearerContractTest" : "HttpBrowserContractTest" })
        {
            var artifact = context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + name + ".java", StringComparison.Ordinal));
            artifact.RelativePath.ShouldContain("src/test/java/");
            var source = Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
            source.ShouldContain("MockMvc");
            source.ShouldNotContain("RuntimeHttp");
            source.ShouldNotContain("__RUNTIME__");
        }
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/HttpBearerTokens.java", StringComparison.Ordinal)).ShouldEqual(bearer);
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/HttpIdentityContractTest.java", StringComparison.Ordinal)).ShouldEqual(!bearer);
    }
}
