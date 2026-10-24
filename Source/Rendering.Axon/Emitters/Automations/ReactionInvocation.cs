// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal static class ReactionInvocation
{
    internal static string? Render(AxonEmissionContext context, SemanticInvocation invocation, ReactionInput input)
    {
        if (!invocation.OnRefused.IsDefaultOrEmpty) return null;
        var located = ScopeSelection.Locate(context.Request.Model.Application).SingleOrDefault(slice => slice.Slice.Commands.Any(command => command.Id == invocation.Command));
        var command = located?.Slice.Commands.Single(candidate => candidate.Id == invocation.Command);
        if (command?.Properties.Any(property => property.IsGenerated) != false) return null;
        var arguments = command.Properties.Select(property => Argument(context, invocation, input, property)).ToArray();
        if (arguments.Any(argument => argument is null)) return null;
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application), located!);
        return $"commands.sendAndWait(new {package}.{JavaNames.Type(command.Name)}({string.Join(", ", arguments)}));";
    }

    internal static void Dispatch(JavaCodeBuilder method, SemanticReaction reaction, string invocation)
    {
        if (reaction.RunsAs is null)
        {
            method.Line(invocation);
            return;
        }
        var roles = reaction.RunsAs.Roles.Distinct(StringComparer.Ordinal)
            .Select(role => "new org.springframework.security.core.authority.SimpleGrantedAuthority(" + JavaCodeBuilder.StringLiteral("ROLE_" + role) + ")");
        method.Line("var previous = org.springframework.security.core.context.SecurityContextHolder.getContext();")
            .Line("var identity = org.springframework.security.core.context.SecurityContextHolder.createEmptyContext();")
            .Line("identity.setAuthentication(new org.springframework.security.authentication.UsernamePasswordAuthenticationToken(\"system\", null, java.util.List.of(" + string.Join(", ", roles) + ")));")
            .Block("try", body => body.Line("org.springframework.security.core.context.SecurityContextHolder.setContext(identity);").Line(invocation))
            .Block("finally", body => body.Line("org.springframework.security.core.context.SecurityContextHolder.setContext(previous);"));
    }

    internal static bool RequiresIdentity(AxonEmissionContext context, SemanticInvocation invocation) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Commands)
            .Any(command => command.Id == invocation.Command && command.Authorization is not null);

    static string? Argument(AxonEmissionContext context, SemanticInvocation invocation, ReactionInput input, SemanticProperty property)
    {
        var mappings = invocation.Mappings.Where(mapping => mapping.TargetProperty == property.Id).ToArray();
        return mappings.Length == 1 ? input.Value(context, mappings[0].Source, property.Type) : null;
    }
}
