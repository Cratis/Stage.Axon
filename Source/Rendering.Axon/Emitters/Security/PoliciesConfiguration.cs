// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class PoliciesConfiguration
{
    internal static void Emit(AxonEmissionContext context)
    {
        // Screenplay 4.250.0 forbids opaque policies under NOT; keep FAILED propagation defensive for every operator.
        var methods = new List<string>();
        foreach (var (policy, condition) in PolicyPredicates.Referenced(context).DistinctBy(item => JavaNames.Member(item.Policy.Name)))
        {
            if (PolicyPredicates.Resolve(context, policy, condition).Body is not { } body) continue;
            EmitPolicy(context, policy, body);
            methods.Add($$"""
                private static Decision evaluate{{JavaNames.Type(policy.Name)}}(Authentication authentication, Map<String, Object> claims) {
                    try {
                        return decision({{PolicyPredicates.PolicyClass(policy)}}.evaluate(authentication, claims));
                    } catch (Throwable failure) {
                        return Decision.FAILED;
                    }
                }
            """);
            context.ConsumedImplementations.Add(condition.RequirementId);
        }
        if (methods.Count == 0) return;
        foreach (var site in PolicyPredicates.Sites(context))
        {
            var expression = PolicyPredicates.Expression(context, site.Authorization);
            if (expression is null) continue;
            methods.Add($$"""
                public boolean {{PolicyPredicates.SiteMember(site.Owner)}}(Authentication authentication) {
                    if (!authenticated(authentication)) return false;
                    try {
                        var claims = claims(authentication);
                        return {{expression}} == Decision.ALLOW;
                    } catch (Throwable failure) {
                        return false;
                    }
                }
            """);
        }
        var bearer = HttpAuthenticationInput.Bearer(context.Request.Profile);
        var code = $$"""
            package {{context.BasePackage}}.infrastructure;

            import java.util.HashMap;
            import java.util.Map;
            import org.springframework.security.authentication.AnonymousAuthenticationToken;
            import org.springframework.security.core.Authentication;
            import org.springframework.stereotype.Component;
            {{(bearer ? "import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;" : string.Empty)}}

            @Component("policies")
            public class Policies {
                private enum Decision { ALLOW, DENY, FAILED }

                private static boolean authenticated(Authentication authentication) {
                    return authentication != null && authentication.isAuthenticated() && !(authentication instanceof AnonymousAuthenticationToken);
                }

                private static Decision decision(boolean allowed) { return allowed ? Decision.ALLOW : Decision.DENY; }
                private static Decision not(Decision value) {
                    return value == Decision.FAILED ? Decision.FAILED : decision(value == Decision.DENY);
                }
                private static Decision and(Decision left, Decision right) {
                    if (left == Decision.FAILED || right == Decision.FAILED) return Decision.FAILED;
                    return decision(left == Decision.ALLOW && right == Decision.ALLOW);
                }
                private static Decision or(Decision left, Decision right) {
                    if (left == Decision.FAILED || right == Decision.FAILED) return Decision.FAILED;
                    return decision(left == Decision.ALLOW || right == Decision.ALLOW);
                }

            {{string.Join("\n\n", methods)}}

                private static Map<String, Object> claims(Authentication authentication) {
                    var claims = new HashMap<String, Object>();
                    {{(bearer ? "if (authentication instanceof JwtAuthenticationToken jwt) claims.putAll(jwt.getTokenAttributes());" : string.Empty)}}
                    if (authentication.getDetails() instanceof Map<?, ?> details) {
                        for (var entry : details.entrySet()) {
                            if (entry.getKey() instanceof String key) claims.putIfAbsent(key, entry.getValue());
                        }
                    }
                    claims.putIfAbsent("sub", authentication.getName());
                    return claims;
                }
            }
            """;
        context.AddJava(context.BasePackage + ".infrastructure", "Policies", code + "\n", sources: [context.Request.Model.Application.Id]);
    }

    static void EmitPolicy(AxonEmissionContext context, SemanticPolicy policy, string body)
    {
        // Bodies are trusted Java, not sandboxed. A separate compilation unit isolates lexical scope from the evaluator and sibling bodies.
        var name = PolicyPredicates.PolicyClass(policy);
        var code = $$"""
            package {{context.BasePackage}}.infrastructure;

            import java.util.Map;
            import org.springframework.security.core.Authentication;

            public final class {{name}} {
                public static boolean evaluate(Authentication authentication, Map<String, Object> claims) throws Exception {
            {{body}}
                }
            }
            """;
        context.AddJava(context.BasePackage + ".infrastructure", name, code + "\n", sources: [context.Request.Model.Application.Id]);
    }
}
