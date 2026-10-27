// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security;

internal static class DemoIdentityRoles
{
    internal static string[] From(AxonEmissionContext context)
    {
        var application = context.Request.Model.Application;
        var slices = ScopeSelection.Locate(application).Select(located => located.Slice).ToArray();
        return [.. application.Policies.SelectMany(policy => From(policy.Condition))
            .Concat(slices.SelectMany(slice => slice.Reactions).SelectMany(reaction => reaction.RunsAs?.Roles ?? []))
            .Concat(slices.SelectMany(slice => slice.Specifications).SelectMany(specification => specification.GivenCaller?.Roles ?? []))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    static IEnumerable<string> From(SemanticPolicyCondition condition) => condition switch
    {
        SemanticRoleCondition role => [role.Role],
        SemanticNotPolicyCondition not => From(not.Operand),
        SemanticLogicalPolicyCondition logical => From(logical.Left).Concat(From(logical.Right)),
        _ => []
    };
}
