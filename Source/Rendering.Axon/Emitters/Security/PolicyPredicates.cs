// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class PolicyPredicates
{
    internal static (string? Body, string Reason) Resolve(AxonEmissionContext context, SemanticPolicy policy, SemanticOpaquePolicyCondition condition)
    {
        var member = JavaNames.Member(policy.Name);
        var first = context.Request.Model.Application.Policies.First(candidate => JavaNames.Member(candidate.Name) == member);
        if (first != policy)
        {
            return (null, $"Policy '{policy.Name}' has a generated Java member collision with '{first.Name}'; access is denied.");
        }
        return InlineJavaBodies.TryResolve(context, new(condition.RequirementId, SemanticImplementationRole.PolicyPredicate, context.Request.Model.Application.Id), out var body, out var reason)
            ? (body, string.Empty) : (null, reason);
    }

    internal static string SiteMember(SemanticId owner) => "authorize" + JavaNames.Type(owner.ToString());

    internal static IEnumerable<(SemanticId Owner, SemanticAuthorization Authorization)> Sites(AxonEmissionContext context) =>
        ScopeSelection.Select(context.Request).SelectMany(located =>
            located.Slice.Commands.Select(command => (command.Id, command.Authorization))
                .Concat(located.Slice.Queries.Select(query => (query.Id, query.Authorization))))
            .Where(site => site.Authorization is not null && HasInline(context, site.Authorization))
            .Select(site => (site.Id, site.Authorization!));

    internal static bool HasInline(AxonEmissionContext context, SemanticAuthorization authorization) =>
        ReferencedPolicies(context, authorization).Any(policy => Opaque(policy.Condition).Any());

    internal static IEnumerable<(SemanticPolicy Policy, SemanticOpaquePolicyCondition Condition)> Referenced(AxonEmissionContext context)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var site in Sites(context)) References(site.Authorization, names);
        foreach (var policy in context.Request.Model.Application.Policies.Where(policy => names.Contains(policy.Name)))
            foreach (var condition in Opaque(policy.Condition)) yield return (policy, condition);
    }

    internal static string? Failure(AxonEmissionContext context, SemanticAuthorization authorization)
    {
        foreach (var policy in ReferencedPolicies(context, authorization))
        {
            foreach (var condition in Opaque(policy.Condition))
            {
                var resolved = Resolve(context, policy, condition);
                if (resolved.Body is null) return resolved.Reason;
            }
        }
        return null;
    }

    internal static string? Expression(AxonEmissionContext context, SemanticAuthorization authorization) => authorization switch
    {
        SemanticPolicyReference reference => context.Request.Model.Application.Policies.FirstOrDefault(policy => policy.Name == reference.Name) is { } policy ? Condition(context, policy, policy.Condition) : null,
        SemanticLogicalAuthorization logical => Combine(Expression(context, logical.Left), logical.Operator, Expression(context, logical.Right)),
        _ => null
    };

    static IEnumerable<SemanticPolicy> ReferencedPolicies(AxonEmissionContext context, SemanticAuthorization authorization)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        References(authorization, names);
        return context.Request.Model.Application.Policies.Where(policy => names.Contains(policy.Name));
    }

    static string? Condition(AxonEmissionContext context, SemanticPolicy policy, SemanticPolicyCondition condition) => condition switch
    {
        SemanticOpaquePolicyCondition opaque => Resolve(context, policy, opaque).Body is not null ? "evaluate" + JavaNames.Type(policy.Name) + "(authentication, claims)" : null,
        _ => DeclarativeCondition(context, policy, condition)
    };

    static string? DeclarativeCondition(AxonEmissionContext context, SemanticPolicy policy, SemanticPolicyCondition condition) => condition switch
    {
        SemanticRoleCondition role => "decision(authentication != null && authentication.getAuthorities().stream().anyMatch(authority -> authority.getAuthority().equals(" + JavaCodeBuilder.StringLiteral("ROLE_" + role.Role) + ")))",
        SemanticAuthenticatedCondition => "decision(authenticated(authentication))",
        SemanticAnonymousCondition => "Decision.ALLOW",
        SemanticNotPolicyCondition not => Condition(context, policy, not.Operand) is { } operand ? "not(" + operand + ")" : null,
        SemanticLogicalPolicyCondition logical => Combine(Condition(context, policy, logical.Left), logical.Operator, Condition(context, policy, logical.Right)),
        _ => null
    };

    static string? Combine(string? left, SemanticLogicalOperator operation, string? right) =>
        left is null || right is null ? null : operation switch
        {
            SemanticLogicalOperator.And => "and(" + left + ", " + right + ")",
            SemanticLogicalOperator.Or => "or(" + left + ", " + right + ")",
            _ => null
        };

    static void References(SemanticAuthorization? authorization, HashSet<string> names)
    {
        switch (authorization)
        {
            case SemanticPolicyReference reference: names.Add(reference.Name); break;
            case SemanticLogicalAuthorization logical:
                References(logical.Left, names);
                References(logical.Right, names);
                break;
        }
    }

    static IEnumerable<SemanticOpaquePolicyCondition> Opaque(SemanticPolicyCondition condition) => condition switch
    {
        SemanticOpaquePolicyCondition opaque => [opaque],
        SemanticNotPolicyCondition not => Opaque(not.Operand),
        SemanticLogicalPolicyCondition logical => Opaque(logical.Left).Concat(Opaque(logical.Right)),
        _ => []
    };
}
