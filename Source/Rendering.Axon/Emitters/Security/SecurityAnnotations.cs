// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class SecurityAnnotations
{
    const string Code = "STAGE-AXON-AUTHORIZATION";

    internal static string For(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner)
    {
        if (authorization is null) return string.Empty;
        var expression = Expression(context, authorization, new(StringComparer.Ordinal));
        if (expression is not null) return "@PreAuthorize(" + JavaCodeBuilder.StringLiteral(expression) + ")";
        var path = owner + "/authorization";
        const string intent = "authorize access with the modeled policy";
        context.Degradation.Unsupported(Code, owner, "The policy requires an unconfigured identity claim or an unsupported condition; access is denied.", intent, path);
        return JavaCodeBuilder.UnsupportedComment(intent, path, Code) + "\n@PreAuthorize(\"denyAll()\")";
    }

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
        var result = policy is null ? null : Condition(policy.Condition);
        visiting.Remove(name);
        return result;
    }

    static string? Condition(SemanticPolicyCondition condition) => condition switch
    {
        SemanticAnonymousCondition => "permitAll()",
        SemanticAuthenticatedCondition => "isAuthenticated()",
        SemanticRoleCondition role => "hasRole('" + role.Role.Replace("'", "''", StringComparison.Ordinal) + "')",
        SemanticNotPolicyCondition not => Condition(not.Operand) is { } operand ? "!(" + operand + ")" : null,
        SemanticLogicalPolicyCondition logical => Combine(Condition(logical.Left), logical.Operator, Condition(logical.Right)),
        _ => null
    };

    static string? Combine(string? left, SemanticLogicalOperator op, string? right) =>
        left is null || right is null ? null : op switch
        {
            SemanticLogicalOperator.And => $"({left}) and ({right})",
            SemanticLogicalOperator.Or => $"({left}) or ({right})",
            _ => null
        };
}
