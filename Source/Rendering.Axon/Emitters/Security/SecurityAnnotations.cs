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
        if (!PolicyPredicates.HasInline(context, authorization)) return PolicyExpressions.Expression(context, authorization, [with(StringComparer.Ordinal)]);
        return PolicyPredicates.Failure(context, authorization) is null ? PolicyPredicates.Expression(context, authorization) : null;
    }

    internal static string ForHandler(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner, JavaCodeBuilder code) =>
        HandlerPolicies.For(context, authorization, owner, code);

    internal static string? UnsupportedReason(AxonEmissionContext context, SemanticAuthorization? authorization, SemanticId owner)
    {
        if (HasNarrowing(context, owner)) return "Policy row narrowing is not rendered; access is denied.";
        if (AvailabilityGates.IsGated(context.Request, owner)) return "Availability gates are not rendered; access is denied.";
        return PolicyPredicates.Unsupported(context, authorization) ? "State policies, reads, typed inputs and input mappings are not rendered; access is denied." : null;
    }

    static bool HasNarrowing(AxonEmissionContext context, SemanticId owner) =>
        ScopeSelection.Select(context.Request).SelectMany(located => located.Slice.Queries).Any(query => query.Id == owner && query.Narrowing is not null);
}
