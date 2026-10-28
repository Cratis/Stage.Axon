// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_AxonSecurityEmitter;

public class when_emitting_the_security_configuration : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _code;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonSecurityEmitter().Emit(context);
        _code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_read_the_cors_allowlist_from_a_property() => _code.ShouldContain("@Value(\"${stage.axon.cors.allowed-origins:}\") String allowedOrigins");
    [Fact] void should_deny_every_origin_when_the_allowlist_is_empty() => _code.ShouldContain("configuration.setAllowedOrigins(Arrays.stream(allowedOrigins.split(\",\")).map(String::trim).filter(origin -> !origin.isEmpty()).toList());");
    [Fact] void should_apply_the_allowlist_to_the_filter_chain() => _code.ShouldContain(".cors(cors -> cors.configurationSource(cors(allowedOrigins)))");
    [Fact] void should_not_allow_ambient_credentials() => _code.ShouldContain("configuration.setAllowCredentials(false);");
    [Fact] void should_allow_the_authorization_header() => _code.ShouldContain("\"Authorization\"");
    [Fact] void should_not_special_case_browser_requests() => _code.ShouldNotContain("Sec-Fetch-Mode");
    [Fact] void should_use_native_challenges() => _code.ShouldContain(".httpBasic(Customizer.withDefaults()).build();");
    [Fact] void should_not_emit_a_custom_entry_point() => _code.ShouldNotContain("entryPoint()");
    [Fact] void should_not_enable_cookie_sessions_or_csrf_tokens() => _code.ShouldContain("csrf.disable()");
    [Fact] void should_not_read_credentials_from_the_url() => _code.ShouldNotContain("getParameter");
    [Fact] void should_not_override_native_identity_providers() => _code.ShouldNotContain("UserDetailsService");
    [Fact] void should_not_qualify_types_inline() => _code.ShouldNotContain("java.util.Arrays.stream");
}
