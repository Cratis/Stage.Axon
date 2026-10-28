// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class DemoIdentityConfiguration
{
    internal static void Emit(AxonEmissionContext context)
    {
        var roles = DemoIdentityRoles.From(context);
        var code = new JavaCodeBuilder(context.BasePackage + ".infrastructure")
            .Import("java.io.IOException")
            .Import("java.util.Arrays")
            .Import("java.util.List")
            .Import("java.util.HashMap")
            .Import("java.util.Map")
            .Import("org.springframework.boot.context.properties.bind.Bindable")
            .Import("org.springframework.boot.context.properties.bind.Binder")
            .Import("org.springframework.core.env.Environment")
            .Import("jakarta.servlet.FilterChain")
            .Import("jakarta.servlet.ServletException")
            .Import("jakarta.servlet.http.HttpServletRequest")
            .Import("jakarta.servlet.http.HttpServletResponse")
            .Import("org.slf4j.LoggerFactory")
            .Import("org.springframework.beans.factory.annotation.Value")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnWebApplication")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.context.annotation.Profile")
            .Import("org.springframework.security.authentication.UsernamePasswordAuthenticationToken")
            .Import("org.springframework.security.config.annotation.web.builders.HttpSecurity")
            .Import("org.springframework.security.config.http.SessionCreationPolicy")
            .Import("org.springframework.security.core.GrantedAuthority")
            .Import("org.springframework.security.core.authority.SimpleGrantedAuthority")
            .Import("org.springframework.security.core.context.SecurityContextHolder")
            .Import("org.springframework.security.web.SecurityFilterChain")
            .Import("org.springframework.security.web.context.SecurityContextHolderFilter")
            .Import("org.springframework.web.cors.CorsConfiguration")
            .Import("org.springframework.web.cors.CorsConfigurationSource")
            .Import("org.springframework.web.cors.UrlBasedCorsConfigurationSource")
            .Import("org.springframework.web.filter.OncePerRequestFilter")
            .Line("@Configuration")
            .Line("@Profile(\"demo\")")
            .Line("@ConditionalOnWebApplication(type = ConditionalOnWebApplication.Type.SERVLET)")
            .Block("public class DemoIdentityConfiguration", body =>
            {
                body.Line("@Bean")
                    .Block("SecurityFilterChain demoSecurity(HttpSecurity http, Environment environment, @Value(\"${stage.axon.cors.allowed-origins:}\") String allowedOrigins) throws Exception", method => Chain(method, roles));
                Cors(body);
                Filter(body);
            }).Build();
        context.AddJava(context.BasePackage + ".infrastructure", "DemoIdentityConfiguration", code);
    }

    static void Chain(JavaCodeBuilder method, string[] roles)
    {
        var authorities = string.Join(", ", roles.Select(role => "new SimpleGrantedAuthority(" + JavaCodeBuilder.StringLiteral("ROLE_" + role) + ")"));
        var warning = "DEMO IDENTITY ENABLED: every request is authenticated as 'demo' with roles [" + string.Join(", ", roles) + "]. Never activate the demo profile in production.";
        method.Line("LoggerFactory.getLogger(DemoIdentityConfiguration.class).warn(" + JavaCodeBuilder.StringLiteral(warning) + ");")
            .Line("List<GrantedAuthority> authorities = List.of(" + authorities + ");")
            .Line("var claims = new HashMap<String, String>(Binder.get(environment).bind(\"stage.axon.demo.claims\", Bindable.mapOf(String.class, String.class)).orElseGet(HashMap::new));")
            .Line("claims.putIfAbsent(\"sub\", \"demo\");")
            .Line("// Explicit demo mode ignores Basic and bearer credentials; method security still enforces access.")
            .Line("return http.csrf(csrf -> csrf.disable())")
            .Line("    .cors(cors -> cors.configurationSource(cors(allowedOrigins)))")
            .Line("    .sessionManagement(session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))")
            .Line("    .authorizeHttpRequests(requests -> requests.anyRequest().permitAll())")
            .Line("    .addFilterAfter(identity(authorities, claims), SecurityContextHolderFilter.class).build();");
    }

    static void Cors(JavaCodeBuilder body) => body.Line()
        .Block("private static CorsConfigurationSource cors(String allowedOrigins)", method => method
            .Line("var configuration = new CorsConfiguration();")
            .Line("configuration.setAllowedOrigins(Arrays.stream(allowedOrigins.split(\",\")).map(String::trim).filter(origin -> !origin.isEmpty()).toList());")
            .Line("configuration.setAllowedMethods(List.of(\"GET\", \"POST\", \"OPTIONS\"));")
            .Line("configuration.setAllowedHeaders(List.of(\"Authorization\", \"Content-Type\", \"Accept\", \"X-Requested-With\"));")
            .Line("configuration.setAllowCredentials(false);")
            .Line("var source = new UrlBasedCorsConfigurationSource();")
            .Line("source.registerCorsConfiguration(\"/**\", configuration);")
            .Line("return source;"));

    static void Filter(JavaCodeBuilder body) => body.Line()
        .Block("private static OncePerRequestFilter identity(List<GrantedAuthority> authorities, Map<String, String> claims)", method => method
            .Line("return new OncePerRequestFilter() {")
            .Line("    @Override")
            .Line("    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain) throws ServletException, IOException {")
            .Line("        var context = SecurityContextHolder.createEmptyContext();")
            .Line("        var token = UsernamePasswordAuthenticationToken.authenticated(\"demo\", null, authorities);")
            .Line("        token.setDetails(claims);")
            .Line("        context.setAuthentication(token);")
            .Line("        SecurityContextHolder.setContext(context);")
            .Line("        try { chain.doFilter(request, response); }")
            .Line("        finally { SecurityContextHolder.clearContext(); }")
            .Line("    }")
            .Line("};"));
}
