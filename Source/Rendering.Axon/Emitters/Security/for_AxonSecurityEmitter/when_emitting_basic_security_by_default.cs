// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_AxonSecurityEmitter;

public class when_emitting_basic_security_by_default : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;
    string identity;
    string metadata;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonSecurityEmitter().Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
        identity = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/IdentityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
        metadata = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/META-INF/spring/org.springframework.boot.autoconfigure.AutoConfiguration.imports").Bytes.AsSpan());
    }

    [Fact] void should_allow_an_application_owned_identity_provider() => identity.ShouldContain("@ConditionalOnMissingBean(UserDetailsService.class)");
    [Fact] void should_reject_unconfigured_identities() => identity.ShouldContain("return username -> { throw new UsernameNotFoundException(\"Configure an identity provider\"); };");
    [Fact] void should_prevent_boot_generated_users_after_application_providers() => identity.ShouldContain("@AutoConfiguration(before = UserDetailsServiceAutoConfiguration.class)");
    [Fact] void should_register_identity_auto_configuration() => metadata.ShouldContain(".infrastructure.IdentityConfiguration");
    [Fact] void should_not_widen_cors_methods() => code.ShouldContain("configuration.setAllowedMethods(Arrays.asList(\"GET\", \"POST\", \"OPTIONS\"));");
    [Fact] void should_authenticate_unregistered_api_routes() => code.ShouldContain(".requestMatchers(\"/api/**\").authenticated()");
    [Fact] void should_preserve_error_dispatches() => code.ShouldContain(".dispatcherTypeMatchers(DispatcherType.ERROR).permitAll()");
    [Fact] void should_preserve_public_non_api_requests() => code.ShouldContain(".requestMatchers(\"/api/**\").authenticated()\n                .anyRequest().permitAll())");
    [Fact] void should_keep_basic() => code.ShouldContain(".httpBasic(Customizer.withDefaults()).build();");
    [Theory]
    [InlineData("oauth2")]
    [InlineData("Jwt")]
    [InlineData("BearerToken")]
    void should_not_include_bearer_configuration(string text) => code.ShouldNotContain(text);
}
