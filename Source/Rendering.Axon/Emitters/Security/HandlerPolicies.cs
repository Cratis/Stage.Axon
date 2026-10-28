// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class HandlerPolicies
{
    internal static string For(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner, JavaCodeBuilder code)
    {
        if (authorization is null) return string.Empty;
        var roles = Roles(context, authorization);
        if (roles is { Length: 0 }) return string.Empty;
        var annotation = "@" + code.Reference("org.springframework.security.access.annotation.Secured");

        // Axon authorizes subscriptions at opening; a denied initial result terminates before any updates.
        if (roles is not null) return annotation + "({" + string.Join(", ", roles.Select(role => JavaCodeBuilder.StringLiteral("ROLE_" + role))) + "})";
        const string intent = "authorize access with the modeled policy";
        var path = owner + "/authorization";
        const string reason = "This handler policy requires claims, custom Authentication fields, or conditions not expressible by native metadata role checks; access is denied.";
        context.Degradation.Unsupported("STAGE-AXON-AUTHORIZATION", owner, reason, intent, path);
        return "// Differs from Screenplay: " + reason + "\n" + JavaCodeBuilder.UnsupportedComment(intent, path, "STAGE-AXON-AUTHORIZATION") + "\n" + annotation + "({})";
    }

    static string[]? Roles(AxonEmissionContext context, SemanticAuthorization authorization) => authorization switch
    {
        SemanticPolicyReference reference => context.Request.Model.Application.Policies.FirstOrDefault(policy => policy.Name == reference.Name) is { } policy ? Condition(policy.Condition) : null,
        SemanticLogicalAuthorization logical => Combine(Roles(context, logical.Left), logical.Operator, Roles(context, logical.Right)),
        _ => null
    };

    static string[]? Condition(SemanticPolicyCondition condition) => condition switch
    {
        SemanticRoleCondition role => [role.Role],
        SemanticAnonymousCondition => [],
        SemanticLogicalPolicyCondition logical => Combine(Condition(logical.Left), logical.Operator, Condition(logical.Right)),
        _ => null
    };

    static string[]? Combine(string[]? left, SemanticLogicalOperator operation, string[]? right)
    {
        if (left is null || right is null) return null;
        if (left.SequenceEqual(right)) return left;
        return operation == SemanticLogicalOperator.Or && left.Length > 0 && right.Length > 0 ? [.. left.Concat(right).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] : null;
    }
}
