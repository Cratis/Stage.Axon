// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_AxonSecurityEmitter;

public class when_emitting_bearer_security : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;
    string guard;

    void Because()
    {
        var bearer = request with { Profile = AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()) };
        var context = new AxonEmissionContext(bearer, new(bearer.Profile));
        new AxonSecurityEmitter().Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
        guard = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/JwtAudienceConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_use_the_resource_server() => code.ShouldContain(".oauth2ResourceServer(server -> server.authenticationEntryPoint(entryPoint()).jwt(jwt -> jwt.decoder(decoders.getIfAvailable(SecurityConfiguration::unconfigured)))).build();");
    [Fact] void should_resolve_the_standard_decoder() => code.ShouldContain("ObjectProvider<JwtDecoder> decoders");
    [Fact] void should_fail_closed_without_a_provider() => code.ShouldContain("throw new BadJwtException(\"Configure an identity provider\")");
    [Fact] void should_check_audiences_at_startup() => guard.ShouldContain("static BeanFactoryPostProcessor jwtAudienceGuard(Environment environment)");
    [Fact] void should_bind_standard_audiences() => guard.ShouldContain("Binder.get(environment).bind(prefix + \"audiences\", String[].class)");
    [Theory]
    [InlineData("issuer-uri")]
    [InlineData("jwk-set-uri")]
    [InlineData("public-key-location")]
    void should_guard_each_decoder_source(string source) => guard.ShouldContain("\"" + source + "\"");
    [Fact] void should_explain_missing_audiences() => guard.ShouldContain("throw new IllegalStateException(\"Configure spring.security.oauth2.resourceserver.jwt.audiences when a JWT decoder source is configured\")");
    [Fact] void should_be_stateless() => code.ShouldContain("session.sessionCreationPolicy(SessionCreationPolicy.STATELESS)");
    [Fact] void should_challenge_other_clients_with_bearer() => code.ShouldContain("var bearer = new BearerTokenAuthenticationEntryPoint();");
    [Fact] void should_delegate_the_challenge() => code.ShouldContain("else bearer.commence(request, response, exception);");
    [Fact] void should_not_challenge_browsers() => code.ShouldContain("request.getHeader(\"Sec-Fetch-Mode\") != null || request.getHeader(\"X-Requested-With\") != null) response.setStatus(401)");
    [Fact] void should_disable_csrf() => code.ShouldContain("csrf.disable()");
    [Fact] void should_keep_the_empty_cors_allowlist() => code.ShouldContain("configuration.setAllowedOrigins(Arrays.stream(allowedOrigins.split(\",\")).map(String::trim).filter(origin -> !origin.isEmpty()).toList());");
    [Fact] void should_keep_the_cors_filter() => code.ShouldContain(".cors(cors -> cors.configurationSource(cors(allowedOrigins)))");
    [Fact] void should_not_allow_cookies() => code.ShouldContain("setAllowCredentials(false)");
    [Fact] void should_keep_the_fail_closed_user_service() => code.ShouldContain("return username -> { throw new UsernameNotFoundException(\"Configure an identity provider\"); };");
    [Fact] void should_not_enable_basic() => code.ShouldNotContain("httpBasic");
    [Fact] void should_not_import_basic() => code.ShouldNotContain("BasicAuthenticationEntryPoint");
    [Fact] void should_use_short_type_names() => code[code.IndexOf("public class", StringComparison.Ordinal)..].ShouldNotContain("org.springframework.security.oauth2.jwt.JwtDecoder");
}
