// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        var code = new JavaCodeBuilder(context.BasePackage + ".infrastructure").Declare("SecurityConfiguration")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.beans.factory.annotation.Value")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean")
            .Import("org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity")
            .Import("org.springframework.security.config.annotation.web.builders.HttpSecurity")
            .Import("org.springframework.security.core.userdetails.UserDetailsService")
            .Import("org.springframework.security.core.userdetails.UsernameNotFoundException")
            .Import("org.springframework.security.web.AuthenticationEntryPoint")
            .Import("org.springframework.security.web.SecurityFilterChain")
            .Import("org.springframework.security.web.authentication.www.BasicAuthenticationEntryPoint")
            .Import("org.springframework.web.cors.CorsConfiguration")
            .Import("org.springframework.web.cors.CorsConfigurationSource")
            .Import("org.springframework.web.cors.UrlBasedCorsConfigurationSource")
            .Line("@Configuration")
            .Line("@EnableMethodSecurity")
            .Block("public class SecurityConfiguration", body => body
                .Line("@Bean")
                .Block("SecurityFilterChain security(HttpSecurity http, @Value(\"${stage.axon.cors.allowed-origins:}\") String allowedOrigins) throws Exception", method => method
                    .Line("return http.csrf(csrf -> csrf.disable())")
                    .Line("    .cors(cors -> cors.configurationSource(cors(allowedOrigins)))")
                    .Line("    .authorizeHttpRequests(requests -> requests.anyRequest().permitAll())")
                    .Line("    .httpBasic(basic -> basic.authenticationEntryPoint(entryPoint())).build();"))
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
                .Block("private static AuthenticationEntryPoint entryPoint()", method => method
                    .Line("var basic = new BasicAuthenticationEntryPoint();")
                    .Line("basic.setRealmName(\"Realm\");")
                    .Line("return (request, response, exception) -> { if (request.getHeader(\"Sec-Fetch-Mode\") != null || request.getHeader(\"X-Requested-With\") != null) response.setStatus(401); else basic.commence(request, response, exception); };"))
                .Line("@Bean")
                .Line("@ConditionalOnMissingBean(UserDetailsService.class)")
                .Block("UserDetailsService users()", method => method
                    .Line("// Fail closed until an application-owned identity provider is supplied.")
                    .Line("return username -> { throw new UsernameNotFoundException(\"Configure an identity provider\"); };")))
            .Build();
        context.AddJava(context.BasePackage + ".infrastructure", "SecurityConfiguration", code);
    }
}
