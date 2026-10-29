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

        // Axon authorizes subscriptions at opening; a denied initial result terminates before any updates.
        if (roles is not null) return "@" + code.Reference("org.springframework.security.access.annotation.Secured") + "({" + string.Join(", ", roles.Select(role => JavaCodeBuilder.StringLiteral("ROLE_" + role))) + "})";
        if (SecurityAnnotations.EndpointExpression(context, authorization) is { } expression)
        {
            // Residual policies evaluate the security context of the thread invoking the handler.
            return "@" + code.Reference("org.springframework.security.access.prepost.PreAuthorize") + "(" + JavaCodeBuilder.StringLiteral(expression) + ")";
        }
        var annotation = "@" + code.Reference("org.springframework.security.access.annotation.Secured");
        const string intent = "authorize access with the modeled policy";
        var path = owner + "/authorization";
        const string reason = "This handler policy requires claims, custom Authentication fields, or conditions not expressible by native metadata role checks; access is denied.";
        context.Degradation.Unsupported("STAGE-AXON-AUTHORIZATION", owner, SecurityAnnotations.SiteName(context, owner) + ": " + reason, intent, path);
        return "// Differs from Screenplay: " + reason + "\n" + JavaCodeBuilder.UnsupportedComment(intent, path, "STAGE-AXON-AUTHORIZATION") + "\n" + annotation + "({})";
    }

    internal static bool UsesMethodSecurity(AxonEmissionContext context, SemanticAuthorization? authorization) =>
        authorization is not null && Roles(context, authorization) is null && SecurityAnnotations.EndpointExpression(context, authorization) is not null;

    static string[]? Roles(AxonEmissionContext context, SemanticAuthorization authorization)
    {
        var terms = Formula(context, authorization);
        if (terms is null) return null;
        if (terms.Any(term => term.Count == 0)) return [];
        return terms.All(term => term.Count == 1) ? [.. terms.SelectMany(term => term).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] : null;
    }

    static HashSet<string>[]? Formula(AxonEmissionContext context, SemanticAuthorization authorization) => authorization switch
    {
        SemanticPolicyReference reference => context.Request.Model.Application.Policies.FirstOrDefault(policy => policy.Name == reference.Name) is { } policy ? Condition(policy.Condition) : null,
        SemanticLogicalAuthorization logical => Combine(Formula(context, logical.Left), logical.Operator, Formula(context, logical.Right)),
        _ => null
    };

    static HashSet<string>[]? Condition(SemanticPolicyCondition condition) => condition switch
    {
        SemanticRoleCondition role => [new([role.Role], StringComparer.Ordinal)],
        SemanticAnonymousCondition => [new(StringComparer.Ordinal)],
        SemanticLogicalPolicyCondition logical => Combine(Condition(logical.Left), logical.Operator, Condition(logical.Right)),
        _ => null
    };

    static HashSet<string>[]? Combine(HashSet<string>[]? left, SemanticLogicalOperator operation, HashSet<string>[]? right)
    {
        if (left is null || right is null) return null;
        const int maximumTerms = 64;
        if (operation == SemanticLogicalOperator.And && left.Length * right.Length > maximumTerms) return null;
        var terms = operation == SemanticLogicalOperator.Or
            ? left.Concat(right)
            : left.SelectMany(first => right.Select(second => new HashSet<string>(first.Concat(second), StringComparer.Ordinal)));
        var reduced = new List<HashSet<string>>();
        foreach (var term in terms)
        {
            // Absorption removes supersets: (Rep or Mgr) and Mgr reduces to Mgr.
            // Native @Secured can represent the result only when all remaining terms are single roles.
            if (reduced.Exists(existing => existing.IsSubsetOf(term))) continue;
            reduced.RemoveAll(term.IsSubsetOf);
            reduced.Add(term);
        }
        return reduced.Count <= maximumTerms ? [.. reduced] : null;
    }
}
