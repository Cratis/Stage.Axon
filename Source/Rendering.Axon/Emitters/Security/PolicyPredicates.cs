// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
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
        if (!InlineJavaBodies.TryResolve(context, new(condition.RequirementId, SemanticImplementationRole.PolicyPredicate, context.Request.Model.Application.Id), out var body, out var reason)) return (null, reason);
        return PolicyBodies.Failure(context, body) is { } failure ? (null, failure) : (body, string.Empty);
    }

    internal static string PolicyClass(SemanticPolicy policy) => JavaNames.Type(policy.Name) + "Policy";

    internal static string BeanName(SemanticPolicy policy) => JavaNames.Member(policy.Name) + "Policy";

    internal static IEnumerable<(SemanticId Owner, SemanticAuthorization Authorization)> Sites(AxonEmissionContext context) =>
        ScopeSelection.Select(context.Request).SelectMany(located =>
            located.Slice.Commands.Select(command => (command.Id, command.Authorization))
                .Concat(located.Slice.Queries.Concat(QueryPerformer.Queries(context, located))
                    .Select(query => (query.Id, query.Authorization))))
            .DistinctBy(site => site.Id)
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
        if (authorization is not SemanticPolicyReference reference || context.Request.Model.Application.Policies.FirstOrDefault(policy => policy.Name == reference.Name)?.Condition is not SemanticOpaquePolicyCondition)
            return "Inline policy composition cannot preserve eager failure-dominance with native Spring expressions; access is denied.";
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

    internal static string? Expression(AxonEmissionContext context, SemanticAuthorization authorization) =>
        authorization is SemanticPolicyReference reference && context.Request.Model.Application.Policies.FirstOrDefault(policy => policy.Name == reference.Name) is { Condition: SemanticOpaquePolicyCondition } policy
            ? "isAuthenticated() and @" + BeanName(policy) + ".check(authentication)" : null;

    static IEnumerable<SemanticPolicy> ReferencedPolicies(AxonEmissionContext context, SemanticAuthorization authorization)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        References(authorization, names);
        return context.Request.Model.Application.Policies.Where(policy => names.Contains(policy.Name));
    }

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
