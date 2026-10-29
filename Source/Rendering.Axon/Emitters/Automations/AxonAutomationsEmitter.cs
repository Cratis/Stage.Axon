// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
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
        var registered = new List<RegisteredReaction>();
        var processors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var located in selected)
        {
            foreach (var reaction in located.Slice.Reactions)
            {
                if (EmitReaction(context, located, reaction, processors) is not { } emitted) continue;
                registered.Add(emitted);
                new ReactionSpecificationsEmitter(context, located, reaction).Emit();
            }
        }
        ApplicationTriggersEmitter.Emit(context, registered);
    }

    static RegisteredReaction? EmitReaction(AxonEmissionContext context, LocatedSemanticSlice located, SemanticReaction reaction, HashSet<string> processors)
    {
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application, context.BasePackage), located);
        var name = JavaNames.Type(reaction.Name) + "Reaction";
        var emitter = new ReactionTriggerEmitter(context, reaction);
        var duplicateEvents = reaction.Triggers.Where(trigger => trigger.Kind == SemanticReactionTriggerKind.Event).GroupBy(trigger => trigger.Source).Any(group => group.Count() > 1);
        var triggers = reaction.Triggers.Select((trigger, index) => duplicateEvents ? null : emitter.Prepare(trigger, index)).ToArray();
        EmitProductions(context, package, reaction.Id, triggers);
        var registered = triggers.All(trigger => trigger is not null);
        if (triggers.Length > 0 && registered && reaction.Triggers.All(trigger => trigger.Kind == SemanticReactionTriggerKind.ApplicationTrigger)) return new(reaction, emitter, triggers!, located.Slice.Name);
        var code = new JavaCodeBuilder(package).Declare(name);
        ReactionJavaReferences.Claim(code, triggers.OfType<PreparedReactionTrigger>().SelectMany(trigger => new[] { trigger.Input.JavaType, trigger.Invocation, trigger.Condition ?? string.Empty }));
        if (registered) code.Line("@" + code.Reference("org.springframework.stereotype.Component"));
        code.Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(package + ".reactions")})")
            .Line("@" + code.Reference("org.axonframework.messaging.eventhandling.replay.annotation.DisallowReplay"))
            .Block($"public class {name}", body =>
            {
                var commands = body.Reference("org.axonframework.messaging.commandhandling.gateway.CommandGateway");
                var usesClock = triggers.Select((trigger, index) => (trigger, index)).Any(entry => entry.trigger is { UsesClock: true } && reaction.Triggers[entry.index].Kind != SemanticReactionTriggerKind.ApplicationTrigger);
                var clock = usesClock ? body.Reference("java.time.Clock") : string.Empty;
                body.Line($"private final {commands} commands;");
                if (usesClock) body.Line($"private final {clock} clock;");
                body.Block($"public {name}({commands} commands{(usesClock ? $", {clock} clock" : string.Empty)})", constructor =>
                {
                    constructor.Line("this.commands = commands;");
                    if (usesClock) constructor.Line("this.clock = clock;");
                });
                EmitMethods(reaction, emitter, triggers, body);
            });
        if (registered && reaction.Triggers.Any(trigger => trigger.Kind == SemanticReactionTriggerKind.Event) && processors.Add(package)) EmitProcessor(context, package);
        if (!registered) context.Degradation.Unsupported("STAGE-AXON-0504", reaction.Id, $"Reaction '{reaction.Name}' is not registered: a trigger's effects, routing, guards or identity cannot be preserved.");
        context.AddJava(package, name, code.Build(), sources: [reaction.Id, .. reaction.Triggers.Where(trigger => trigger.Source != default).Select(trigger => trigger.Source)]);
        return registered ? new(reaction, emitter, triggers!, located.Slice.Name) : null;
    }

    // One definition per reaction namespace: a reaction sends commands, so its processor starts at the latest token and never reacts to history.
    static void EmitProcessor(AxonEmissionContext context, string package)
    {
        var code = new JavaCodeBuilder(package).Declare("ReactionsProcessorConfiguration");
        var definition = code.Reference("org.axonframework.extension.spring.config.EventProcessorDefinition");
        var namespaceName = JavaCodeBuilder.StringLiteral(package + ".reactions");
        code.Line("@" + code.Reference("org.springframework.context.annotation.Configuration") + "(value = " + JavaCodeBuilder.StringLiteral(package + ".reactions.configuration") + ", proxyBeanMethods = false)")
            .Block("class ReactionsProcessorConfiguration", body => body.Line("@" + body.Reference("org.springframework.context.annotation.Bean") + "(" + namespaceName + ")")
                .Block($"{definition} processor()", method => method.Line($"return {definition}.pooledStreamingMatching(" + namespaceName + ").customized(config -> config.initialToken(source -> source.latestToken(null)));")));
        context.AddJava(package, "ReactionsProcessorConfiguration", code.Build());
    }

    static void EmitMethods(SemanticReaction reaction, ReactionTriggerEmitter emitter, PreparedReactionTrigger?[] triggers, JavaCodeBuilder body)
    {
        var unregistered = triggers.Any(trigger => trigger is null);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < triggers.Length; index++)
        {
            if (triggers[index] is not { } trigger || (unregistered && trigger.Production is not null))
            {
                body.UnsupportedIntent($"execute reaction {reaction.Name} trigger {index + 1} with its original guards, identity, mappings and effects", reaction.Id.ToString(), "STAGE-AXON-0504")
                    .DiffersFromScreenplay($"Reaction {reaction.Name} trigger {index + 1} is not rendered because its effects, routing, guards or identity cannot be preserved.")
                    .Block($"private void unsupportedTrigger{index + 1}()", method => method.Line("throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral($"Reaction {reaction.Name} trigger {index + 1} is not supported") + ");"));
                continue;
            }
            var model = reaction.Triggers[index];
            if (model.Kind == SemanticReactionTriggerKind.ApplicationTrigger) continue;
            var name = emitter.MethodName(model, trigger, index);
            if (!names.Add(name)) names.Add(name += index + 1);
            emitter.Emit(model, trigger, index, name, body);
        }
    }

    static void EmitProductions(AxonEmissionContext context, string package, SemanticId reaction, PreparedReactionTrigger?[] triggers)
    {
        if (triggers.Any(trigger => trigger is null)) return;
        foreach (var production in triggers.Select(trigger => trigger!.Production).OfType<ReactionProduction>()) production.Emit(context, package, reaction);
    }
}

internal sealed record RegisteredReaction(SemanticReaction Reaction, ReactionTriggerEmitter Emitter, PreparedReactionTrigger[] Triggers, string Slice);
