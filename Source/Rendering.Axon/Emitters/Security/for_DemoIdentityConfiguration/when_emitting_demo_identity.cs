// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_DemoIdentityConfiguration;

public class when_emitting_demo_identity : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_emit_an_isolated_profile_with_all_distinct_modeled_roles(bool bearer)
    {
        var application = request.Model.Application with
        {
            CommandReadMode = SemanticCommandReadMode.Protected,
            Policies = [.. request.Model.Application.Policies, new("Read", new SemanticRoleCondition("Reader")), new("Write", new SemanticLogicalPolicyCondition(new SemanticRoleCondition("Writer"), SemanticLogicalOperator.Or, new SemanticNotPolicyCondition(new SemanticRoleCondition("Reader"))))]
        };
        var configured = request with { Model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, application), Profile = bearer ? AxonRendering.WithBearerTokens(request.Profile) : request.Profile };
        var context = new AxonEmissionContext(configured, new(configured.Profile));
        new AxonSecurityEmitter().Emit(context);
        var demo = Source(context, "DemoIdentityConfiguration");
        demo.ShouldContain("@Profile(\"demo\")");
        demo.ShouldContain("new SimpleGrantedAuthority(\"ROLE_Reader\"), new SimpleGrantedAuthority(\"ROLE_Sales\"), new SimpleGrantedAuthority(\"ROLE_Writer\")");
        demo.ShouldContain("UsernamePasswordAuthenticationToken.authenticated(\"demo\", null, authorities)");
        demo.ShouldContain("SecurityContextHolder.createEmptyContext()");
        demo.ShouldContain("finally { SecurityContextHolder.clearContext(); }");
        demo.ShouldContain(".addFilterAfter(identity(authorities), SecurityContextHolderFilter.class)");
        demo.ShouldContain(".warn(\"DEMO IDENTITY ENABLED:");
        demo.ShouldContain("Never activate the demo profile in production.");
        demo.ShouldContain("SessionCreationPolicy.STATELESS");
        demo.ShouldContain("configuration.setAllowCredentials(false)");
        demo[demo.IndexOf("public class", StringComparison.Ordinal)..].ShouldNotContain("org.springframework.");
        demo[demo.IndexOf("public class", StringComparison.Ordinal)..].ShouldNotContain("java.util.");
        demo.ShouldNotContain(".httpBasic(");
        demo.ShouldNotContain(".oauth2ResourceServer(");
        Source(context, "SecurityConfiguration").ShouldContain("@Profile(\"!demo\")");
        Source(context, "SecurityConfiguration").ShouldContain("@EnableMethodSecurity");
    }

    static string Source(AxonEmissionContext context, string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
