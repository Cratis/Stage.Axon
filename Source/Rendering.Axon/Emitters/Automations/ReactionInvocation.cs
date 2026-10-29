// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
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
        if (ConstraintCoverage.CommandRequiresUnsupportedConstraint(context, command)) return null;
        var arguments = command.Properties.Select(property => Argument(context, invocation, input, property)).ToArray();
        if (arguments.Any(argument => argument is null)) return null;
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application, context.BasePackage), located!);
        return $"new {package}.{JavaNames.Type(command.Name)}({string.Join(", ", arguments)})";
    }

    internal static string Statement(string command, bool withContext, bool withMetadata = false)
    {
        if (withMetadata) return $"commands.sendAndWait({command}, java.lang.Object.class, metadata, {(withContext ? "context" : "null")});";
        return $"commands.sendAndWait({command}{(withContext ? ", context" : string.Empty)});";
    }

    internal static void Dispatch(JavaCodeBuilder method, SemanticReaction reaction, string invocation, bool http = false)
        => Dispatch(method, reaction, body => body.Line(invocation), http);

    internal static void Dispatch(JavaCodeBuilder method, SemanticReaction reaction, Action<JavaCodeBuilder> effect, bool http = false)
    {
        if (reaction.RunsAs is null)
        {
            effect(method);
            return;
        }
        var authorities = string.Join(',', reaction.RunsAs.Roles.Select(role => "ROLE_" + role).Distinct(StringComparer.Ordinal));
        method.Line($"{(http ? "metadata" : "var metadata")} = {method.Reference("org.axonframework.messaging.core.Metadata")}.with(\"username\", \"system\").and(\"authorities\", {JavaCodeBuilder.StringLiteral(authorities)});");

        // Retained for the current actor expressions (Caller.current()), not for native handler authorization.
        var roles = reaction.RunsAs.Roles.Distinct(StringComparer.Ordinal)
            .Select(role => $"new {method.Reference("org.springframework.security.core.authority.SimpleGrantedAuthority")}(" + JavaCodeBuilder.StringLiteral("ROLE_" + role) + ")");
        var holder = method.Reference("org.springframework.security.core.context.SecurityContextHolder");
        method.Line($"var previous = {holder}.getContext();")
            .Line($"var identity = {holder}.createEmptyContext();")
            .Line($"identity.setAuthentication(new {method.Reference("org.springframework.security.authentication.UsernamePasswordAuthenticationToken")}(\"system\", null, {method.Reference("java.util.List")}.of(" + string.Join(", ", roles) + ")));")
            .Block("try", body =>
            {
                body.Line($"{holder}.setContext(identity);");
                effect(body);
            })
            .Block("finally", body => body.Line($"{holder}.setContext(previous);"));
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
