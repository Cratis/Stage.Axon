// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

/// <summary>Compiles named Screenplay policies into Spring Security expressions.</summary>
internal static class PolicyExpressions
{
    internal static string? Expression(AxonEmissionContext context, SemanticAuthorization authorization, HashSet<string> visiting) => authorization switch
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
