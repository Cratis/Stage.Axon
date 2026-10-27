// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_emitting_a_bearer_scaffold : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string basicPom;

    void Because()
    {
        var bearer = request with { Profile = AxonRendering.WithBearerTokens(request.Profile) };
        context = new AxonEmissionContext(bearer, new(bearer.Profile));
        new AxonScaffoldEmitter().Emit(context);
        var basic = new AxonEmissionContext(request, new(request.Profile));
        new AxonScaffoldEmitter().Emit(basic);
        basicPom = Encoding.UTF8.GetString(basic.Artifacts.Single(artifact => artifact.RelativePath == "pom.xml").Bytes.AsSpan());
    }

    [Fact] void should_add_the_boot_four_resource_server() => Source("pom.xml").ShouldContain("<dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-security-oauth2-resource-server</artifactId></dependency>");
    [Fact] void should_not_add_oauth_by_default() => basicPom.ShouldNotContain("oauth2");
    [Fact] void should_configure_the_roles_claim() => Source("src/main/resources/application.yml").ShouldContain("  security:\n    oauth2:\n      resourceserver:\n        jwt:\n          authorities-claim-name: roles\n          authority-prefix: ROLE_\n");
    [Theory]
    [InlineData("Authorization")]
    [InlineData("spring.security.oauth2.resourceserver.jwt.issuer-uri")]
    [InlineData("jwk-set-uri")]
    [InlineData("public-key-location")]
    [InlineData("spring.security.oauth2.resourceserver.jwt.audiences")]
    [InlineData("every token is rejected")]
    [InlineData("roles")]
    [InlineData("authority-prefix")]
    void should_document_bearer_configuration(string text) => Source("README.md").ShouldContain(text);

    string Source(string path) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
