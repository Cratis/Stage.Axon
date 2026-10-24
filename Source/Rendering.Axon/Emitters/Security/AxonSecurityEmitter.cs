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
        var code = new JavaCodeBuilder(context.BasePackage + ".infrastructure")
            .Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean")
            .Import("org.springframework.security.config.Customizer")
            .Import("org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity")
            .Import("org.springframework.security.config.annotation.web.builders.HttpSecurity")
            .Import("org.springframework.security.core.userdetails.UserDetailsService")
            .Import("org.springframework.security.core.userdetails.UsernameNotFoundException")
            .Import("org.springframework.security.web.SecurityFilterChain")
            .Line("@Configuration")
            .Line("@EnableMethodSecurity")
            .Block("public class SecurityConfiguration", body => body
                .Line("@Bean")
                .Block("SecurityFilterChain security(HttpSecurity http) throws Exception", method => method
                    .Line("return http.csrf(csrf -> csrf.disable())")
                    .Line("    .authorizeHttpRequests(requests -> requests.anyRequest().permitAll())")
                    .Line("    .httpBasic(Customizer.withDefaults()).build();"))
                .Line("@Bean")
                .Line("@ConditionalOnMissingBean(UserDetailsService.class)")
                .Block("UserDetailsService users()", method => method
                    .Line("// Fail closed until an application-owned identity provider is supplied.")
                    .Line("return username -> { throw new UsernameNotFoundException(\"Configure an identity provider\"); };")))
            .Build();
        context.AddJava(context.BasePackage + ".infrastructure", "SecurityConfiguration", code);
    }
}
