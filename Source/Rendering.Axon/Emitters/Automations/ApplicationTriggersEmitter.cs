// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal static class ApplicationTriggersEmitter
{
    internal static void Emit(AxonEmissionContext context, IReadOnlyList<(string Type, Cratis.Screenplay.Semantics.SemanticReaction Reaction)> reactions)
    {
        var package = context.BasePackage + ".triggers";
        var types = new CommandJavaTypes(context.Request.Model.Application);
        foreach (var trigger in context.Request.Model.Application.Triggers)
        {
            var handlers = reactions.SelectMany(reaction => reaction.Reaction.Triggers.Select((candidate, index) => (reaction.Type, Trigger: candidate, Index: index)))
                .Where(candidate => candidate.Trigger.Kind == Cratis.Screenplay.Semantics.SemanticReactionTriggerKind.ApplicationTrigger && candidate.Trigger.Source == trigger.Id).ToArray();
            if (handlers.Length == 0) continue;
            var name = JavaNames.Type(trigger.Name);
            var code = new JavaCodeBuilder(package).Declare(name);
            ReactionJavaReferences.Claim(code, trigger.Properties.Select(property => types.Type(property.Type)));
            var properties = trigger.Properties.Select(property => (property.Type.IsOptional ? string.Empty : $"@{code.Reference("jakarta.validation.constraints.NotNull")} ") + $"@{code.Reference("jakarta.validation.Valid")} " + ReactionJavaReferences.Expression(code, types.Type(property.Type)) + " " + JavaNames.Member(property.Name));
            context.AddJava(package, name, code.Line($"public record {name}({string.Join(", ", properties)}) {{}}").Build(), sources: [trigger.Id]);
            context.AddJava(package, name + "Endpoint", Endpoint(package, trigger.Name, handlers), sources: [trigger.Id]);
        }
    }

    static string Endpoint(string package, string triggerName, (string Type, Cratis.Screenplay.Semantics.SemanticReactionTrigger Trigger, int Index)[] handlers)
    {
        var name = JavaNames.Type(triggerName);
        var components = handlers.Select(handler => handler.Type).Distinct(StringComparer.Ordinal).ToArray();
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Endpoint");
        ReactionJavaReferences.Claim(code, components);
        code.Line("@" + code.Reference("org.springframework.web.bind.annotation.RestController"))
                .Block($"public class {name}Endpoint", body =>
                {
                    for (var index = 0; index < components.Length; index++) body.Line($"private final {ReactionJavaReferences.Expression(body, components[index])} reaction{index};");
                    body.Block($"public {name}Endpoint({string.Join(", ", components.Select((type, index) => ReactionJavaReferences.Expression(body, type) + " reaction" + index))})", constructor =>
                    {
                        for (var index = 0; index < components.Length; index++) constructor.Line($"this.reaction{index} = reaction{index};");
                    }).Line($"@{body.Reference("org.springframework.security.access.prepost.PreAuthorize")}(\"hasRole('SYSTEM')\")")
                        .Line($"@{body.Reference("org.springframework.web.bind.annotation.PostMapping")}(" + JavaCodeBuilder.StringLiteral("/api/triggers/" + CommandEndpointEmitter.Kebab(triggerName)) + ")")
                        .Block($"public void execute(@{body.Reference("jakarta.validation.Valid")} @{body.Reference("org.springframework.web.bind.annotation.RequestBody")} {name} input)", method =>
                        {
                            foreach (var handler in handlers) method.Line($"reaction{Array.IndexOf(components, handler.Type)}.on{handler.Index}(input);");
                        });
                });
        return code.Build();
    }
}
