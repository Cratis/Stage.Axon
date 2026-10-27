// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal static class CommandFixtureSecurity
{
    internal static void Caller(JavaCodeBuilder method, SemanticCaller? caller) => CallerCore(method, caller, null);

    internal static void CallerReferences(JavaCodeBuilder method, SemanticCaller? caller) => CallerCore(method, caller, method);

    internal static void ReportUnsupportedClaims(AxonEmissionContext context, JavaCodeBuilder code, SemanticSpecification specification)
    {
        var caller = specification.GivenCaller;
        if (caller is not { Claims.IsDefaultOrEmpty: false }) return;
        if (caller.Authenticated && HasOnlySubjectClaim(caller)) return;
        const string diagnostic = "STAGE-AXON-SPEC-CLAIMS";
        const string intent = "preserve all modeled caller claims in the security fixture";
        var path = specification.Id + "/givenCaller/claims";
        context.Degradation.Unsupported(diagnostic, specification.Id, "Caller fixtures preserve only the first sub or nameidentifier claim for an authenticated caller; other claims are not represented.", intent, path);
        code.UnsupportedIntent(intent, path, diagnostic);
    }

    internal static void Proxy(JavaCodeBuilder body, string name, bool usesClock) => ProxyCore(body, name, usesClock, null);

    internal static void ProxyReferences(JavaCodeBuilder body, string name, bool usesClock) => ProxyCore(body, name, usesClock, body);

    static string Reference(JavaCodeBuilder? code, string type) => code?.Reference(type) ?? type;

    static bool HasOnlySubjectClaim(SemanticCaller caller)
    {
        if (caller.Claims.Length != 1) return false;
        var type = caller.Claims[0].Type;
        return type == "sub" || type.EndsWith("/nameidentifier", StringComparison.Ordinal);
    }

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
