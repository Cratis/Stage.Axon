// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class SecurityAnnotations
{
    const string Code = "STAGE-AXON-AUTHORIZATION";

    internal static string For(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner)
    {
        if (UnsupportedReason(context, authorization, owner) is { } unsupported)
        {
            context.Degradation.Unsupported(Code, owner, unsupported, "authorize access with the modeled policy", owner + "/authorization");
            return "@PreAuthorize(\"denyAll()\")";
        }
        if (authorization is null) return string.Empty;
        var inline = PolicyPredicates.HasInline(context, authorization);
        var expression = EndpointExpression(context, authorization);
        if (expression is not null) return "@PreAuthorize(" + JavaCodeBuilder.StringLiteral(expression) + ")";
        var path = owner + "/authorization";
        const string intent = "authorize access with the modeled policy";
        var reason = "The policy requires an unconfigured identity claim or an unsupported condition; access is denied.";
        if (inline && PolicyPredicates.Failure(context, authorization) is { } detail) reason += " " + SiteName(context, owner) + ": " + detail;
        context.Degradation.Unsupported(Code, owner, reason, intent, path);
        return JavaCodeBuilder.UnsupportedComment(intent, path, Code) + "\n@PreAuthorize(\"denyAll()\")";
    }

    internal static string SiteName(AxonEmissionContext context, SemanticId owner) => ScopeSelection.Select(context.Request)
        .SelectMany(slice => slice.Slice.Commands.Select(command => (command.Id, command.Name))
            .Concat(slice.Slice.Queries.Select(query => (query.Id, query.Name))))
        .FirstOrDefault(site => site.Id == owner).Name ?? owner.ToString();

    internal static string? EndpointExpression(AxonEmissionContext context, SemanticAuthorization authorization)
    {
        if (PolicyPredicates.Unsupported(context, authorization)) return null;
        if (!PolicyPredicates.HasInline(context, authorization)) return Expression(context, authorization, [with(StringComparer.Ordinal)]);
        return PolicyPredicates.Failure(context, authorization) is null ? PolicyPredicates.Expression(context, authorization) : null;
    }

    internal static string ForHandler(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner, JavaCodeBuilder code) =>
        HandlerPolicies.For(context, authorization, owner, code);

    internal static string? UnsupportedReason(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner)
    {
        if (HasNarrowing(context, owner)) return "Policy row narrowing is not rendered; access is denied.";
        return PolicyPredicates.Unsupported(context, authorization) ? "State policies, reads, typed inputs and input mappings are not rendered; access is denied." : null;
    }

    static bool HasNarrowing(AxonEmissionContext context, SemanticId owner) =>
        ScopeSelection.Select(context.Request).SelectMany(located => located.Slice.Queries).Any(query => query.Id == owner && query.Narrowing is not null);

    static string? Expression(AxonEmissionContext context, SemanticAuthorization authorization, HashSet<string> visiting) => authorization switch
    {
        SemanticPolicyReference reference => Policy(context, reference.Name, visiting),
        SemanticLogicalAuthorization logical => Combine(Expression(context, logical.Left, visiting), logical.Operator, Expression(context, logical.Right, visiting)),
        _ => null
    };

    static string? Policy(AxonEmissionContext context, string name, HashSet<string> visiting)
    {
        if (!visiting.Add(name)) return null;
        var policy = context.Request.Model.Application.Policies.FirstOrDefault(policy => policy.Name == name);
        var result = policy is null ? null : Condition(context, policy, policy.Condition);
        visiting.Remove(name);
        return result;
    }

    static string? Condition(AxonEmissionContext context, SemanticPolicy policy, SemanticPolicyCondition condition) => condition switch
    {
        SemanticAnonymousCondition => "permitAll()",
        SemanticAuthenticatedCondition => "isAuthenticated()",
        SemanticRoleCondition role => "hasRole('" + role.Role.Replace("'", "''", StringComparison.Ordinal) + "')",
        SemanticNotPolicyCondition not => Condition(context, policy, not.Operand) is { } operand ? "!(" + operand + ")" : null,
        SemanticLogicalPolicyCondition logical => Combine(Condition(context, policy, logical.Left), logical.Operator, Condition(context, policy, logical.Right)),
        _ => null
    };

    static string? Combine(string? left, SemanticLogicalOperator op, string? right) =>
        left is null || right is null ? null : op switch
        {
            SemanticLogicalOperator.And => left == right ? left : $"({left}) and ({right})",
            SemanticLogicalOperator.Or => left == right ? left : $"({left}) or ({right})",
            _ => null
        };
}
