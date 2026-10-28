// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionTriggerEmitter(AxonEmissionContext context, SemanticReaction reaction)
{
    internal PreparedReactionTrigger? Prepare(SemanticReactionTrigger trigger, int index)
    {
        if (!SupportedEffects(trigger) || !SupportedIdentity(trigger)) return null;
        var annotation = ReactionScheduling.Annotation(trigger);
        var input = ReactionInput.Create(context, trigger);
        if (annotation is null || input is null) return null;
        var prepared = trigger.RequirementId is not null ? PrepareNative(trigger, annotation, input) : PrepareDeclarative(trigger, index, annotation, input);
        if (prepared is null || trigger.Kind is not (SemanticReactionTriggerKind.Interval or SemanticReactionTriggerKind.Schedule)) return prepared;
        return context.Degradation.Approximate(
            AxonRefusalCodes.Schedule,
            reaction.Id,
            "Schedules use UTC and process-local Spring scheduling, not a durable scheduler; interval boundaries start with this process.",
            $"run reaction {reaction.Name} trigger {index + 1} on its modeled clock",
            $"{reaction.Id}/triggers/{index}") ? prepared : null;
    }

    internal void Emit(SemanticReactionTrigger model, PreparedReactionTrigger trigger, int index, JavaCodeBuilder body)
    {
        if (trigger.Annotation.Length > 0) body.Line(ReactionJavaReferences.Expression(body, trigger.Annotation));
        var parameters = trigger.Input.JavaType.Length == 0 ? string.Empty : ReactionJavaReferences.Expression(body, trigger.Input.JavaType) + " input";
        if (trigger.Input.Event is not null) parameters += $", @{body.Reference("org.axonframework.messaging.eventhandling.annotation.Timestamp")} {body.Reference("java.time.Instant")} occurred";
        body.Block($"public void on{index}({parameters})", method =>
        {
            ReactionScheduling.CalendarGuard(method, model);
            if (trigger.Condition is { } condition) method.Line($"if (!({ReactionJavaReferences.Expression(method, condition)})) return;");
            if (model.Kind == SemanticReactionTriggerKind.Interval)
                method.DiffersFromScreenplay("Spring fixed-rate intervals start when this process starts, not at epoch-aligned Screenplay clock boundaries; delivery remains at least once.");
            Effect(method, trigger);
        });
        if (trigger.NativeBody is not null) context.ConsumedImplementations.Add(model.RequirementId!);
    }

    static bool StandaloneNativeEffect(SemanticReactionTrigger trigger) => trigger.Produces.IsEmpty && trigger.Invokes.IsEmpty;

    static bool StandaloneDeclarativeEffect(SemanticReactionTrigger trigger) =>
        (trigger.Produces.IsEmpty && trigger.Invokes.Length == 1) || (trigger.Invokes.IsEmpty && trigger.Produces.Length == 1);

    PreparedReactionTrigger? PrepareDeclarative(SemanticReactionTrigger trigger, int index, string annotation, ReactionInput input)
    {
        var production = trigger.Produces.IsEmpty ? null : new ReactionProductionPreparation(context, trigger.Produces[0], input).Prepare(JavaNames.Type(reaction.Name) + "On" + index + "Append");
        var invocation = trigger.Invokes.IsEmpty ? production?.Invocation : ReactionInvocation.Render(context, trigger.Invokes[0], input);
        if (invocation is null) return null;
        return WithGuard(trigger, new(annotation, input, invocation, null) { Production = production });
    }

    PreparedReactionTrigger? PrepareNative(SemanticReactionTrigger trigger, string annotation, ReactionInput input)
    {
        var slice = ScopeSelection.Locate(context.Request.Model.Application).FirstOrDefault(located => located.Slice.Reactions.Any(candidate => candidate.Id == reaction.Id))?.Slice.Id;
        var slot = new InlineBodySlot(trigger.RequirementId, SemanticImplementationRole.ReactionEffect, reaction.Id, slice, reaction.Name + "/");
        if (!InlineJavaBodies.TryResolve(context, slot, out var content, out _)) return null;
        return WithGuard(trigger, new(annotation, input, string.Empty, null) { NativeBody = content });
    }

    void Effect(JavaCodeBuilder method, PreparedReactionTrigger trigger)
    {
        if (trigger.NativeBody is { } content) ReactionInvocation.Dispatch(method, reaction, body => body.RawBody(content));
        else ReactionInvocation.Dispatch(method, reaction, ReactionJavaReferences.Expression(method, trigger.Invocation));
    }

    PreparedReactionTrigger? WithGuard(SemanticReactionTrigger trigger, PreparedReactionTrigger prepared)
    {
        if (trigger.Where is null) return prepared;
        var condition = prepared.Input.Condition(context, trigger.Where);
        return condition is null ? null : prepared with { Condition = condition };
    }

    bool SupportedIdentity(SemanticReactionTrigger trigger)
    {
        if (reaction.RunsAs is { Kind: not SemanticReactionIdentityKind.System }) return false;
        if (trigger.Invokes.IsEmpty || reaction.RunsAs is not null) return true;
        if (!ReactionInvocation.RequiresIdentity(context, trigger.Invokes[0])) return true;
        context.Degradation.Unsupported("STAGE-AXON-0505", reaction.Id, $"Reaction '{reaction.Name}' must declare a trusted identity before invoking an authorized command.");
        return false;
    }

    bool SupportedEffects(SemanticReactionTrigger trigger)
    {
        if (reaction.From is not null) return false;
        return trigger.RequirementId is not null ? StandaloneNativeEffect(trigger) : StandaloneDeclarativeEffect(trigger);
    }
}
