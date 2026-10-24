// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal static class CommandFixtureSecurity
{
    internal static void Caller(JavaCodeBuilder method, SemanticCaller? caller)
    {
        const string holder = "org.springframework.security.core.context.SecurityContextHolder";
        method.Line($"{holder}.setContext({holder}.createEmptyContext());");
        if (caller?.Authenticated != true)
        {
            method.Line($"{holder}.getContext().setAuthentication(new org.springframework.security.authentication.AnonymousAuthenticationToken(\"stage-spec\", \"anonymous\", java.util.List.of(new org.springframework.security.core.authority.SimpleGrantedAuthority(\"ROLE_ANONYMOUS\"))));");
            return;
        }
        var identity = caller.Claims.FirstOrDefault(claim => claim.Type == "sub" || claim.Type.EndsWith("/nameidentifier", StringComparison.Ordinal))?.Value ?? "stage-spec-caller";
        var roles = caller.Roles.Select(role => $"new org.springframework.security.core.authority.SimpleGrantedAuthority({JavaCodeBuilder.StringLiteral("ROLE_" + role)})");
        method.Line($"{holder}.getContext().setAuthentication(org.springframework.security.authentication.UsernamePasswordAuthenticationToken.authenticated({JavaCodeBuilder.StringLiteral(identity)}, null, java.util.List.of({string.Join(", ", roles)})));");
    }

    internal static void Proxy(JavaCodeBuilder body, string name)
    {
        body.Block($"private {name}Handler handler(java.time.Clock clock)", method => method
            .Line($"var factory = new org.springframework.aop.framework.ProxyFactory(new {name}Handler(new {name}Rules(validators.getValidator()), clock));")
            .Line("factory.setProxyTargetClass(true);")
            .Line("factory.addAdvisor(org.springframework.security.authorization.method.AuthorizationManagerBeforeMethodInterceptor.preAuthorize());")
            .Line($"return ({name}Handler) factory.getProxy();"));
    }
}
