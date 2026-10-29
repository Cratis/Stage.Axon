// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_AxonSecurityEmitter;

public class when_emitting_bearer_security : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;
    AxonEmissionContext context;

    void Because()
    {
        var bearer = request with { Profile = AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()) };
        context = new(bearer, new(bearer.Profile));
        new AxonSecurityEmitter().Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_use_the_native_resource_server() => code.ShouldContain(".oauth2ResourceServer(server -> server.jwt(jwt -> jwt.decoder(decoder(environment, decoders))))");
    [Fact] void should_resolve_the_standard_decoder() => code.ShouldContain("ObjectProvider<JwtDecoder> decoders");
    [Fact] void should_reject_credentials_without_a_provider() => code.ShouldContain("throw new BadJwtException(\"Configure a JWT provider and spring.security.oauth2.resourceserver.jwt.audiences\")");
    [Fact] void should_require_native_audience_configuration() => code.ShouldContain("Binder.get(environment).bind(\"spring.security.oauth2.resourceserver.jwt.audiences\", String[].class)");
    [Fact] void should_reject_when_audiences_are_absent() => code.ShouldContain("if (Arrays.stream(audiences).noneMatch(audience -> !audience.isBlank())) return unconfigured();");
    [Fact] void should_document_the_rejecting_stub() => context.Degradation.UnsupportedConstructs.Any(item => item.Code == "STAGE-AXON-JWT-PROVIDER").ShouldBeTrue();
    [Fact] void should_not_emit_a_startup_refusal() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/JwtAudienceConfiguration.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_be_stateless() => code.ShouldContain("session.sessionCreationPolicy(SessionCreationPolicy.STATELESS)");
    [Fact] void should_use_native_challenges() => code.ShouldNotContain("Sec-Fetch-Mode");
    [Fact] void should_not_disable_native_providers() => code.ShouldNotContain("UserDetailsService");
    [Fact] void should_disable_csrf() => code.ShouldContain("csrf.disable()");
    [Fact] void should_keep_the_cors_filter() => code.ShouldContain(".cors(cors -> cors.configurationSource(cors(allowedOrigins)))");
    [Fact] void should_not_allow_cookies() => code.ShouldContain("setAllowCredentials(false)");
    [Fact] void should_not_enable_basic() => code.ShouldNotContain("httpBasic");
    [Fact] void should_register_native_command_dispatch() => code.ShouldContain("MessageAuthorizationDispatchInterceptor<CommandMessage>");
    [Fact] void should_register_native_query_dispatch() => code.ShouldContain("MessageAuthorizationDispatchInterceptor<QueryMessage>");
    [Fact] void should_register_native_command_handling() => code.ShouldContain("MessageAuthorizationHandlerInterceptor<CommandMessage>");
    [Fact] void should_register_native_query_handling() => code.ShouldContain("MessageAuthorizationHandlerInterceptor<QueryMessage>");
}
