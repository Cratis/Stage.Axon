// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_authorization_reaches_generated_ingress : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan _plan;
    ArtifactRenderPlan _withoutAuthorization;

    void Because()
    {
        _plan = new AxonArtifactRenderPlanner().Plan(request);
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Commands = [.. slice.Commands.Select(command => command with { Authorization = null })],
                Queries = [.. slice.Queries.Select(query => query with { Authorization = null })]
            })]
        };
        var application = loaded.Model.Application with
        {
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, application);
        _withoutAuthorization = new AxonArtifactRenderPlanner().Plan(new(model, SemanticExecutionPlan.Compile(model).Plan, request.Profile, new(ArtifactRenderScopeKind.Application, application.Id)));
    }

    [Fact]
    void should_match_command_endpoint_and_handler_authorization()
    {
        Source("PlaceOrderEndpoint").ShouldContain("@PreAuthorize(\"hasRole('Sales')\")");
        Source("PlaceOrderHandler").ShouldContain("@Secured({\"ROLE_Sales\"})");
    }

    [Fact]
    void should_match_snapshot_endpoint_and_handler_authorization()
    {
        Source("GetOrderEndpoint").ShouldContain("@PreAuthorize(\"hasRole('Sales')\")");
        Source("GetOrderHandler").ShouldContain("@Secured({\"ROLE_Sales\"})");
    }

    [Fact]
    void should_authorize_live_stream_opening() =>
        Source("AllOrdersEndpoint").ShouldContain("@PreAuthorize(\"hasRole('Sales')\")");

    [Fact]
    void should_require_authentication_for_commands_and_queries_before_request_binding()
    {
        var security = Source("SecurityConfiguration");
        security.ShouldContain(".requestMatchers(\"/api/**\").authenticated()");
        security.ShouldNotContain("\"/api/orders/orders/place-order\"");
        security.ShouldNotContain("\"/api/orders/orders/get-order\"");
        security.ShouldNotContain("\"/api/orders/orders/all-orders\"");
    }

    [Fact]
    void should_preserve_implicit_anonymous_routes_at_the_filter_level()
    {
        var security = Encoding.UTF8.GetString(_withoutAuthorization.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
        security.ShouldContain(".requestMatchers(HttpMethod.POST, \"/api/orders/orders/place-order\").permitAll()");
        security.ShouldContain(".requestMatchers(HttpMethod.GET, \"/api/orders/orders/get-order\").permitAll()");
        security.ShouldContain(".requestMatchers(HttpMethod.HEAD, \"/api/orders/orders/get-order\").permitAll()");
        security.ShouldContain(".requestMatchers(\"/api/**\").authenticated()");
    }

    [Fact]
    void should_only_exempt_whole_permit_all_expressions_before_the_api_default()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        AxonHttpTestsEndpoints.Register(context, new("query", "GET", "/api/anonymous", null, "permitAll()", [], null));
        AxonHttpTestsEndpoints.Register(context, new("query", "GET", "/api/composed", null, "(permitAll()) or (hasRole('Sales'))", [], null));
        new AxonSecurityEmitter().Emit(context);
        var security = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/SecurityConfiguration.java", StringComparison.Ordinal)).Bytes.AsSpan());
        security.ShouldContain(".requestMatchers(HttpMethod.GET, \"/api/anonymous\").permitAll()");
        security.ShouldContain(".requestMatchers(HttpMethod.HEAD, \"/api/anonymous\").permitAll()");
        security.ShouldNotContain("\"/api/composed\"");
        security.IndexOf("\"/api/anonymous\"", StringComparison.Ordinal).ShouldBeLessThan(security.IndexOf("\"/api/**\"", StringComparison.Ordinal));
    }

    [Fact]
    void should_enable_spring_method_security() => Source("SecurityConfiguration").ShouldContain("@EnableMethodSecurity");

    [Fact]
    void should_only_require_http_security_in_servlet_contexts() => Source("SecurityConfiguration").ShouldContain(
        "@ConditionalOnWebApplication(type = ConditionalOnWebApplication.Type.SERVLET)\n    SecurityFilterChain chain(HttpSecurity http, ");

    [Fact]
    void should_keep_method_security_enabled_in_non_web_contexts() => Source("SecurityConfiguration").ShouldContain(
        "@Configuration\n@EnableMethodSecurity\npublic class SecurityConfiguration");

    [Fact]
    void should_leave_identity_providers_to_native_configuration() => Source("SecurityConfiguration").ShouldNotContain("UserDetailsService");

    [Fact]
    void should_not_add_authorization_where_none_was_modeled()
    {
        foreach (var name in new[] { "PlaceOrderEndpoint", "PlaceOrderHandler", "GetOrderEndpoint", "GetOrderHandler", "AllOrdersEndpoint" })
        {
            Encoding.UTF8.GetString(_withoutAuthorization.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan()).ShouldNotContain("@PreAuthorize");
        }
    }

    string Source(string name) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
