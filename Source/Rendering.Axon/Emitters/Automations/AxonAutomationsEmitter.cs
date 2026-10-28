// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class AxonAutomationsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var selected = ScopeSelection.Select(context.Request);
        var registered = new List<(string Type, SemanticReaction Reaction)>();
        foreach (var located in selected)
        {
            foreach (var reaction in located.Slice.Reactions)
            {
                if (!EmitReaction(context, located, reaction)) continue;
                registered.Add((CommandContractsEmitter.Package(new(context.Request.Model.Application, context.BasePackage), located) + "." + JavaNames.Type(reaction.Name) + "Reaction", reaction));
                new ReactionSpecificationsEmitter(context, located, reaction).Emit();
            }
        }
        ApplicationTriggersEmitter.Emit(context, registered);
        if (registered.Count == 0) return;
        context.AddJava(context.BasePackage + ".infrastructure", "ReactionConfiguration", new JavaCodeBuilder(context.BasePackage + ".infrastructure").Declare("ReactionConfiguration")
            .Import("org.springframework.context.annotation.Configuration")
            .Import("org.springframework.scheduling.annotation.EnableScheduling")
            .Import("org.springframework.context.annotation.PropertySource")
            .Line("@Configuration")
            .Line("@EnableScheduling")
            .Line("@PropertySource(\"classpath:stage-reactions.properties\")")
            .Block("public class ReactionConfiguration", _ => { }).Build());
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/stage-reactions.properties", "axon.eventhandling.processors.reactions.mode=pooled\naxon.eventhandling.processors.reactions.initial-segment-count=1\n"));
    }

    static bool EmitReaction(AxonEmissionContext context, LocatedSemanticSlice located, SemanticReaction reaction)
    {
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application, context.BasePackage), located);
        var name = JavaNames.Type(reaction.Name) + "Reaction";
        var emitter = new ReactionTriggerEmitter(context, reaction);
        var duplicateEvents = reaction.Triggers.Where(trigger => trigger.Kind == SemanticReactionTriggerKind.Event).GroupBy(trigger => trigger.Source).Any(group => group.Count() > 1);
        var triggers = reaction.Triggers.Select((trigger, index) => duplicateEvents ? null : emitter.Prepare(trigger, index)).ToArray();
        EmitProductions(context, package, reaction.Id, triggers);
        var code = new JavaCodeBuilder(package).Declare(name);
        ReactionJavaReferences.Claim(code, triggers.OfType<PreparedReactionTrigger>().SelectMany(trigger => new[] { trigger.Input.JavaType, trigger.Invocation, trigger.Condition ?? string.Empty }));
        if (triggers.All(trigger => trigger is not null)) code.Line("@" + code.Reference("org.springframework.stereotype.Component"));
        code.Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}(\"reactions\")")
            .Line("@" + code.Reference("org.axonframework.messaging.eventhandling.replay.annotation.DisallowReplay"))
            .Block($"public class {name}", body =>
            {
                body.Line($"private final {body.Reference("org.axonframework.messaging.commandhandling.gateway.CommandGateway")} commands;")
                    .Line($"private final {body.Reference("java.time.Clock")} clock;")
                    .Block($"public {name}({body.Reference("org.axonframework.messaging.commandhandling.gateway.CommandGateway")} commands, {body.Reference("java.time.Clock")} clock)", constructor => constructor.Line("this.commands = commands;").Line("this.clock = clock;"));
                var unregistered = triggers.Any(trigger => trigger is null);
                for (var index = 0; index < triggers.Length; index++)
                {
                    if (triggers[index] is not { } trigger || (unregistered && trigger.Production is not null))
                    {
                        var cause = emitter.Rejections.TryGetValue(index, out var rejected) ? ": " + rejected : string.Empty;
                        body.UnsupportedIntent($"execute reaction {reaction.Name} trigger {index + 1} with its original guards, identity, mappings and effects{cause}", reaction.Id.ToString(), "STAGE-AXON-0504");
                        continue;
                    }
                    emitter.Emit(reaction.Triggers[index], trigger, index, body);
                }
            });
        if (triggers.Any(trigger => trigger is null)) context.Degradation.Unsupported("STAGE-AXON-0504", reaction.Id, $"Reaction '{reaction.Name}' is not registered: a trigger's effects, routing, guards or identity cannot be preserved." + Causes(emitter));
        context.AddJava(package, name, code.Build(), sources: [reaction.Id, .. reaction.Triggers.Where(trigger => trigger.Source != default).Select(trigger => trigger.Source)]);
        return triggers.All(trigger => trigger is not null);
    }

    static string Causes(ReactionTriggerEmitter emitter) =>
        string.Concat(emitter.Rejections.Select(rejection => $" Trigger {rejection.Key + 1} implementation body: {rejection.Value}"));

    static void EmitProductions(AxonEmissionContext context, string package, SemanticId reaction, PreparedReactionTrigger?[] triggers)
    {
        if (triggers.Any(trigger => trigger is null)) return;
        foreach (var production in triggers.Select(trigger => trigger!.Production).OfType<ReactionProduction>()) production.Emit(context, package, reaction);
    }
}
