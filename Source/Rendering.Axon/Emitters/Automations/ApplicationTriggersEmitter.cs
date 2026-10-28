// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal static class ApplicationTriggersEmitter
{
    internal static void Emit(AxonEmissionContext context, IReadOnlyList<RegisteredReaction> reactions)
    {
        var package = context.BasePackage + ".triggers";
        var types = new CommandJavaTypes(context.Request.Model.Application, context.BasePackage);
        foreach (var trigger in context.Request.Model.Application.Triggers)
        {
            var handlers = reactions.SelectMany(reaction => reaction.Reaction.Triggers.Select((candidate, index) => (Registered: reaction, Trigger: candidate, Index: index)))
                .Where(candidate => candidate.Trigger.Kind == SemanticReactionTriggerKind.ApplicationTrigger && candidate.Trigger.Source == trigger.Id).ToArray();
            if (handlers.Length == 0) continue;
            var name = JavaNames.Type(trigger.Name);
            var code = new JavaCodeBuilder(package).Declare(name);
            ReactionJavaReferences.Claim(code, trigger.Properties.Select(property => types.Type(property.Type)));
            var properties = trigger.Properties.Select(property => (property.Type.IsOptional ? string.Empty : $"@{code.Reference("jakarta.validation.constraints.NotNull")} ") + $"@{code.Reference("jakarta.validation.Valid")} " + JavaJsonNames.Annotation(property.Name, code) + ReactionJavaReferences.Expression(code, types.Type(property.Type)) + " " + JavaNames.Member(property.Name));
            context.AddJava(package, name, code.Line($"public record {name}({string.Join(", ", properties)}) {{}}").Build(), sources: [trigger.Id]);
            context.AddJava(package, name + "Endpoint", Endpoint(package, trigger.Name, [.. handlers.Select(handler => (handler.Registered, handler.Index))]), sources: [trigger.Id, .. handlers.Select(handler => handler.Registered.Reaction.Id)]);
        }
    }

    static string Endpoint(string package, string triggerName, (RegisteredReaction Registered, int Index)[] handlers)
    {
        var name = JavaNames.Type(triggerName);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Endpoint");
        ReactionJavaReferences.Claim(code, handlers.SelectMany(handler => new[] { handler.Registered.Triggers[handler.Index].Invocation, handler.Registered.Triggers[handler.Index].Condition ?? string.Empty }));
        var methods = handlers.Select(handler => "run" + JavaNames.Type(handler.Registered.Reaction.Name) + (handlers.Count(other => other.Registered == handler.Registered) > 1 ? handler.Index + 1 : string.Empty)).ToArray();
        var usesClock = handlers.Any(handler => handler.Registered.Triggers[handler.Index].UsesClock);
        code.Line("@" + code.Reference("org.springframework.web.bind.annotation.RestController"))
            .Block($"public class {name}Endpoint", body =>
            {
                var commands = body.Reference("org.axonframework.messaging.commandhandling.gateway.CommandGateway");
                var clock = usesClock ? body.Reference("java.time.Clock") : string.Empty;
                body.Line($"private final {commands} commands;");
                if (usesClock) body.Line($"private final {clock} clock;");
                body.Block($"public {name}Endpoint({commands} commands{(usesClock ? $", {clock} clock" : string.Empty)})", constructor =>
                {
                    constructor.Line("this.commands = commands;");
                    if (usesClock) constructor.Line("this.clock = clock;");
                }).Line($"@{body.Reference("org.springframework.security.access.prepost.PreAuthorize")}(\"hasRole('SYSTEM')\")")
                    .Line($"@{body.Reference("org.springframework.web.bind.annotation.PostMapping")}(" + JavaCodeBuilder.StringLiteral("/api/triggers/" + CommandEndpointEmitter.Kebab(triggerName)) + ")")
                    .Block($"public void execute(@{body.Reference("jakarta.validation.Valid")} @{body.Reference("org.springframework.web.bind.annotation.RequestBody")} {name} input)", method =>
                    {
                        foreach (var runner in methods) method.Line($"{runner}(input);");
                    });
                for (var index = 0; index < handlers.Length; index++)
                {
                    var (registered, trigger) = handlers[index];
                    registered.Emitter.EmitApplicationEffect(registered.Reaction.Triggers[trigger], registered.Triggers[trigger], methods[index], name, body);
                }
            });
        return code.Build();
    }
}
