// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal sealed class AxonSecurityEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        foreach (var slice in ScopeSelection.Select(context.Request))
        {
            foreach (var command in slice.Slice.Commands) SecurityAnnotations.For(context, command.Authorization, command.Id);
            foreach (var query in slice.Slice.Queries) SecurityAnnotations.For(context, query.Authorization, query.Id);
        }
        var bearer = HttpAuthenticationInput.Bearer(context.Request.Profile);
        EmitConfiguration(context, bearer);
        DemoIdentityConfiguration.Emit(context);
        if (bearer) EmitAudienceGuard(context);
    }

    static void EmitConfiguration(AxonEmissionContext context, bool bearer)
    {
        var builder = new JavaCodeBuilder(context.BasePackage + ".infrastructure").Declare("SecurityConfiguration")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.context.annotation.Profile")
            .Import("org.springframework.beans.factory.annotation.Value")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnWebApplication")
            .Import("org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity")
            .Import("org.springframework.security.config.annotation.web.builders.HttpSecurity")
            .Import("org.springframework.security.core.userdetails.UserDetailsService")
            .Import("org.springframework.security.core.userdetails.UsernameNotFoundException")
            .Import("org.springframework.security.web.AuthenticationEntryPoint")
            .Import("org.springframework.security.web.SecurityFilterChain")
            .Import(bearer ? "org.springframework.security.oauth2.server.resource.web.BearerTokenAuthenticationEntryPoint" : "org.springframework.security.web.authentication.www.BasicAuthenticationEntryPoint")
            .Import("org.springframework.web.cors.CorsConfiguration")
            .Import("org.springframework.web.cors.CorsConfigurationSource")
            .Import("org.springframework.web.cors.UrlBasedCorsConfigurationSource");
        if (bearer)
        {
            builder.Import("org.springframework.beans.factory.ObjectProvider")
                .Import("org.springframework.security.config.http.SessionCreationPolicy")
                .Import("org.springframework.security.oauth2.jwt.JwtDecoder")
                .Import("org.springframework.security.oauth2.jwt.BadJwtException");
        }
        var code = builder.Line("@Configuration")
            .Line("@EnableMethodSecurity")
            .Block("public class SecurityConfiguration", body =>
            {
                body.Line("@Bean")
                .Line("@Profile(\"!demo\")")
                .Line("@ConditionalOnWebApplication(type = ConditionalOnWebApplication.Type.SERVLET)")
                .Block("SecurityFilterChain security(HttpSecurity http, @Value(\"${stage.axon.cors.allowed-origins:}\") String allowedOrigins" + (bearer ? ", ObjectProvider<JwtDecoder> decoders" : string.Empty) + ") throws Exception", method => Chain(method, bearer))
                .Line()
                .Line("// Cross-origin access is denied unless origins are listed in stage.axon.cors.allowed-origins (comma separated).")
                .Block("private static CorsConfigurationSource cors(String allowedOrigins)", method => method
                    .Line("var configuration = new CorsConfiguration();")
                    .Line($"configuration.setAllowedOrigins({method.Reference("java.util.Arrays")}.stream(allowedOrigins.split(\",\")).map(String::trim).filter(origin -> !origin.isEmpty()).toList());")
                    .Line($"configuration.setAllowedMethods({method.Reference("java.util.List")}.of(\"GET\", \"POST\", \"OPTIONS\"));")
                    .Line($"configuration.setAllowedHeaders({method.Reference("java.util.List")}.of(\"Authorization\", \"Content-Type\", \"Accept\", \"X-Requested-With\"));")
                    .Line("configuration.setAllowCredentials(false);")
                    .Line("var source = new UrlBasedCorsConfigurationSource();")
                    .Line("source.registerCorsConfiguration(\"/**\", configuration);")
                    .Line("return source;"))
                .Line()
                .Line("// Browsers (fetch, XHR, navigation) send Sec-Fetch-Mode or X-Requested-With; they get a plain 401 so no credential prompt appears.")
                .Block("private static AuthenticationEntryPoint entryPoint()", method => Challenge(method, bearer))
                .Line("@Bean")
                .Line("@ConditionalOnMissingBean(UserDetailsService.class)")
                .Block("UserDetailsService users()", method => method
                    .Line("// Fail closed until an application-owned identity provider is supplied.")
                    .Line("return username -> { throw new UsernameNotFoundException(\"Configure an identity provider\"); };"));
                if (bearer)
                {
                    body.Line("// Every bearer token is rejected until JWT properties or an application JwtDecoder names the identity provider.")
                        .Block("private static JwtDecoder unconfigured()", method => method
                            .Line("return token -> { throw new BadJwtException(\"Configure an identity provider\"); };"));
                }
            })
            .Build();
        context.AddJava(context.BasePackage + ".infrastructure", "SecurityConfiguration", code);
    }

    static void EmitAudienceGuard(AxonEmissionContext context)
    {
        var code = new JavaCodeBuilder(context.BasePackage + ".infrastructure")
            .Import("java.util.Arrays")
            .Import("org.springframework.beans.factory.config.BeanFactoryPostProcessor")
            .Import("org.springframework.boot.context.properties.bind.Binder")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.core.env.Environment")
            .Line("@Configuration")
            .Block("public class JwtAudienceConfiguration", AudienceGuard)
            .Build();
        context.AddJava(context.BasePackage + ".infrastructure", "JwtAudienceConfiguration", code);
    }

    static void AudienceGuard(JavaCodeBuilder body)
    {
        body.Line("@Bean")
            .Block("static BeanFactoryPostProcessor jwtAudienceGuard(Environment environment)", method => method
                .Line("return beanFactory -> requireJwtAudience(environment);"))
            .Block("private static void requireJwtAudience(Environment environment)", method => method
                .Line("var prefix = \"spring.security.oauth2.resourceserver.jwt.\";")
                .Line("var configured = Arrays.stream(new String[] { \"issuer-uri\", \"jwk-set-uri\", \"public-key-location\" })")
                .Line("    .anyMatch(name -> !environment.getProperty(prefix + name, \"\").isBlank());")
                .Line("var audiences = Binder.get(environment).bind(prefix + \"audiences\", String[].class).orElse(new String[0]);")
                .Line("if (configured && Arrays.stream(audiences).noneMatch(audience -> !audience.isBlank()))")
                .Line("    throw new IllegalStateException(\"Configure spring.security.oauth2.resourceserver.jwt.audiences when a JWT decoder source is configured\");"));
    }

    static void Chain(JavaCodeBuilder method, bool bearer)
    {
        method.Line("return http.csrf(csrf -> csrf.disable())")
            .Line("    .cors(cors -> cors.configurationSource(cors(allowedOrigins)))");
        if (bearer) method.Line("    .sessionManagement(session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))");
        method.Line("    .authorizeHttpRequests(requests -> requests.anyRequest().permitAll())")
            .Line(bearer
                ? "    .oauth2ResourceServer(server -> server.authenticationEntryPoint(entryPoint()).jwt(jwt -> jwt.decoder(decoders.getIfAvailable(SecurityConfiguration::unconfigured)))).build();"
                : "    .httpBasic(basic -> basic.authenticationEntryPoint(entryPoint())).build();");
    }

    static void Challenge(JavaCodeBuilder method, bool bearer)
    {
        if (bearer) method.Line("var bearer = new BearerTokenAuthenticationEntryPoint();");
        else method.Line("var basic = new BasicAuthenticationEntryPoint();").Line("basic.setRealmName(\"Realm\");");
        method.Line("return (request, response, exception) -> { if (request.getHeader(\"Sec-Fetch-Mode\") != null || request.getHeader(\"X-Requested-With\") != null) response.setStatus(401); else " + (bearer ? "bearer" : "basic") + ".commence(request, response, exception); };");
    }
}
