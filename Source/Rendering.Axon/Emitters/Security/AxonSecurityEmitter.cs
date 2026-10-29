// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
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
        if (bearer)
        {
            context.Degradation.Unsupported("STAGE-AXON-JWT-PROVIDER", context.Request.Model.Application.Id, "Credentials are rejected unless a native JWT decoder and nonempty spring.security.oauth2.resourceserver.jwt.audiences are configured.", "validate tokens for the configured audience", "application/authentication");
        }
        EmitConfiguration(context, bearer);
        EmitUsers(context);
        PoliciesConfiguration.Emit(context);
    }

    static void EmitConfiguration(AxonEmissionContext context, bool bearer)
    {
        var code = new JavaCodeBuilder(context.BasePackage + ".infrastructure")
            .Import("java.util.Arrays")
            .Import("org.springframework.beans.factory.annotation.Value")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnWebApplication")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity")
            .Import("org.springframework.security.config.annotation.web.builders.HttpSecurity")
            .Import("org.springframework.security.web.SecurityFilterChain")
            .Import("org.springframework.web.cors.CorsConfiguration")
            .Import("org.springframework.web.cors.CorsConfigurationSource")
            .Import("org.springframework.web.cors.UrlBasedCorsConfigurationSource");
        if (bearer)
        {
            code.Import("org.springframework.beans.factory.ObjectProvider").Import("org.springframework.core.env.Environment")
                .Import("org.springframework.boot.context.properties.bind.Binder").Import("org.springframework.security.oauth2.jwt.JwtDecoder")
                .Import("org.springframework.security.oauth2.jwt.BadJwtException").Import("org.springframework.security.config.http.SessionCreationPolicy");
        }
        else
        {
            code.Import("org.springframework.security.config.Customizer");
        }
        code.Line("@Configuration").Line("@EnableMethodSecurity").Block("public class SecurityConfiguration", body =>
        {
            body.Line("@Bean").Line("@ConditionalOnWebApplication(type = ConditionalOnWebApplication.Type.SERVLET)").Block("SecurityFilterChain chain(HttpSecurity http, @Value(\"${stage.axon.cors.allowed-origins:}\") String allowedOrigins" + (bearer ? ", Environment environment, ObjectProvider<JwtDecoder> decoders" : string.Empty) + ") throws Exception", method =>
            {
                method.Line("return http.csrf(csrf -> csrf.disable())").Line("    .cors(cors -> cors.configurationSource(cors(allowedOrigins)))");
                if (bearer) method.Line("    .sessionManagement(session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))");
                method.Line("    .authorizeHttpRequests(requests -> requests.anyRequest().permitAll())")
                    .Line(bearer ? "    .oauth2ResourceServer(server -> server.jwt(jwt -> jwt.decoder(decoder(environment, decoders)))).build();" : "    .httpBasic(Customizer.withDefaults()).build();");
            }).Block("private static CorsConfigurationSource cors(String allowedOrigins)", method => method
                .Line("var configuration = new CorsConfiguration();")
                .Line("configuration.setAllowedOrigins(Arrays.stream(allowedOrigins.split(\",\")).map(String::trim).filter(origin -> !origin.isEmpty()).toList());")
                .Line("configuration.setAllowedMethods(Arrays.asList(\"GET\", \"POST\", \"OPTIONS\"));")
                .Line("configuration.setAllowedHeaders(Arrays.asList(\"Authorization\", \"Content-Type\"));")
                .Line("configuration.setAllowCredentials(false);")
                .Line("var source = new UrlBasedCorsConfigurationSource();")
                .Line("source.registerCorsConfiguration(\"/**\", configuration);")
                .Line("return source;"));
            if (bearer) Decoder(body);
            Interceptors(body, "Command", "org.axonframework.messaging.commandhandling.CommandMessage");
            Interceptors(body, "Query", "org.axonframework.messaging.queryhandling.QueryMessage");
        });
        context.AddJava(context.BasePackage + ".infrastructure", "SecurityConfiguration", code.Build());
    }

    static void EmitUsers(AxonEmissionContext context)
    {
        var package = context.BasePackage + ".infrastructure";
        var code = new JavaCodeBuilder(package)
            .Import("org.springframework.boot.autoconfigure.AutoConfiguration")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean")
            .Import("org.springframework.boot.security.autoconfigure.UserDetailsServiceAutoConfiguration")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.security.core.userdetails.UserDetailsService")
            .Import("org.springframework.security.core.userdetails.UsernameNotFoundException");
        code.Line("@AutoConfiguration(before = UserDetailsServiceAutoConfiguration.class)")
            .Block("public class IdentityConfiguration", body => body.Line("@Bean").Line("@ConditionalOnMissingBean(UserDetailsService.class)")
                .Block("UserDetailsService users()", method => method
                    .Line("// Fail closed until an application-owned identity provider is supplied.")
                    .Line("return username -> { throw new UsernameNotFoundException(\"Configure an identity provider\"); };")));
        context.AddJava(package, "IdentityConfiguration", code.Build());
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/META-INF/spring/org.springframework.boot.autoconfigure.AutoConfiguration.imports", package + ".IdentityConfiguration\n"));
    }

    static void Decoder(JavaCodeBuilder body)
    {
        body.Line(JavaCodeBuilder.UnsupportedComment("validate tokens for the configured audience", "application/authentication", "STAGE-AXON-JWT-PROVIDER"))
            .Block("private static JwtDecoder decoder(Environment environment, ObjectProvider<JwtDecoder> decoders)", method => method
                .Line("var audiences = Binder.get(environment).bind(\"spring.security.oauth2.resourceserver.jwt.audiences\", String[].class).orElse(new String[0]);")
                .Line("if (Arrays.stream(audiences).noneMatch(audience -> !audience.isBlank())) return unconfigured();")
                .Line("return decoders.getIfAvailable(SecurityConfiguration::unconfigured);"))
            .Block("private static JwtDecoder unconfigured()", method => method
                .Line("return token -> { throw new BadJwtException(\"Configure a JWT provider and spring.security.oauth2.resourceserver.jwt.audiences\"); };"));
    }

    static void Interceptors(JavaCodeBuilder body, string name, string messageType)
    {
        var message = body.Reference(messageType);

        // AF's dispatch authorization interceptor serializes the whole principal; controllers supply name-only metadata instead.
        var handler = body.Reference("org.axonframework.extension.spring.authorization.MessageAuthorizationHandlerInterceptor");
        body.Line("@Bean").Block($"{handler}<{message}> authorize{name}Handling()", method => method.Line($"return new {handler}<>();"));
    }
}
