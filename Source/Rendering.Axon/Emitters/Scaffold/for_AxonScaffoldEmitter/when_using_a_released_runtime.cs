// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_using_a_released_runtime : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData("9.9.9")]
    [InlineData("9.9.9-preview.1")]
    void should_emit_neither_a_warning_nor_a_comment(string version)
    {
        var context = new AxonEmissionContext(request, new AxonDegradation(request.Profile));
        AxonScaffoldEmitter.Emit(context, version);
        context.Degradation.Warnings.ShouldBeEmpty();
        var pom = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "pom.xml").Bytes.AsSpan());
        pom.ShouldNotContain("<!--");
        pom.ShouldContain($"<stage-axon-runtime.version>{version}</stage-axon-runtime.version>");
    }

    // Hashes of the orders pom with its runtime pin replaced by 9.9.9; re-pinned when spring-boot-starter-data-jpa-test joined the test scope.
    [Theory]
    [InlineData(false, "28D72B83C00464B87AB6B940B510C46DFAE6C4037E323008F05B62A188DE6208")]
    [InlineData(true, "6218F903D673FB4BA51A04C16BC9DFF01D7D1B901B8E5548C7FC3D4178EB3F77")]
    void should_preserve_every_byte_of_the_released_pom(bool bearer, string expectedHash)
    {
        var pom = AxonMavenProject.Pom("corpusorders", "corpusorders-application", bearer, "9.9.9");
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pom))).ShouldEqual(expectedHash);
    }
}
