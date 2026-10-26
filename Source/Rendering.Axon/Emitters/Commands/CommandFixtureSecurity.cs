// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal static class CommandFixtureSecurity
{
    internal static void Caller(JavaCodeBuilder method, SemanticCaller? caller) => CallerCore(method, caller, null);

    internal static void CallerReferences(JavaCodeBuilder method, SemanticCaller? caller) => CallerCore(method, caller, method);

    internal static void Proxy(JavaCodeBuilder body, string name, bool usesClock) => ProxyCore(body, name, usesClock, null);

    internal static void ProxyReferences(JavaCodeBuilder body, string name, bool usesClock) => ProxyCore(body, name, usesClock, body);

    static string Reference(JavaCodeBuilder? code, string type) => code?.Reference(type) ?? type;

    static void CallerCore(JavaCodeBuilder method, SemanticCaller? caller, JavaCodeBuilder? code)
    {
        var holder = Reference(code, "org.springframework.security.core.context.SecurityContextHolder");
        method.Line($"{holder}.setContext({holder}.createEmptyContext());");
        if (caller?.Authenticated != true)
        {
            method.Line($"{holder}.getContext().setAuthentication(new {Reference(code, "org.springframework.security.authentication.AnonymousAuthenticationToken")}(\"stage-spec\", \"anonymous\", {Reference(code, "java.util.List")}.of(new {Reference(code, "org.springframework.security.core.authority.SimpleGrantedAuthority")}(\"ROLE_ANONYMOUS\"))));");
            return;
        }
        var identity = caller.Claims.FirstOrDefault(claim => claim.Type == "sub" || claim.Type.EndsWith("/nameidentifier", StringComparison.Ordinal))?.Value ?? "stage-spec-caller";
        var roles = caller.Roles.Select(role => $"new {Reference(code, "org.springframework.security.core.authority.SimpleGrantedAuthority")}({JavaCodeBuilder.StringLiteral("ROLE_" + role)})");
        method.Line($"{holder}.getContext().setAuthentication({Reference(code, "org.springframework.security.authentication.UsernamePasswordAuthenticationToken")}.authenticated({JavaCodeBuilder.StringLiteral(identity)}, null, {Reference(code, "java.util.List")}.of({string.Join(", ", roles)})));");
    }

    static void ProxyCore(JavaCodeBuilder body, string name, bool usesClock, JavaCodeBuilder? code)
    {
        var parameter = usesClock ? $"{Reference(code, "java.time.Clock")} clock" : string.Empty;
        var argument = usesClock ? ", clock" : string.Empty;
        body.Block($"private {name}Handler handler({parameter})", method => method
            .Line($"var factory = new {Reference(code, "org.springframework.aop.framework.ProxyFactory")}(new {name}Handler(new {name}Rules(validators.getValidator()){argument}));")
            .Line("factory.setProxyTargetClass(true);")
            .Line($"factory.addAdvisor({Reference(code, "org.springframework.security.authorization.method.AuthorizationManagerBeforeMethodInterceptor")}.preAuthorize());")
            .Line($"return ({name}Handler) factory.getProxy();"));
    }
}
