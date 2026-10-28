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

    // Hashes of the pre-change orders pom, with only its runtime pin replaced by 9.9.9.
    [Theory]
    [InlineData(false, "8E586A3C60CD3655AAA45068552A91C459B084446677882A9D69401E632080E8")]
    [InlineData(true, "51D09CA81484EF5B9F4F73D8EC513870A4CE264E9305B08351DB4A5747F0558F")]
    void should_preserve_every_byte_of_the_released_pom(bool bearer, string expectedHash)
    {
        var pom = AxonMavenProject.Pom("corpusorders", "corpusorders-application", bearer, "9.9.9");
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pom))).ShouldEqual(expectedHash);
    }
}
