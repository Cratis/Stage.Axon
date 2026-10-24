// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionTriggerEmitter(AxonEmissionContext context, SemanticReaction reaction)
{
    internal PreparedReactionTrigger? Prepare(SemanticReactionTrigger trigger, int index)
    {
        if (!SupportedEffects(trigger) || !SupportedIdentity(trigger)) return null;
        var annotation = ReactionScheduling.Annotation(trigger);
        var input = ReactionInput.Create(context, trigger);
        if (annotation is null || input is null) return null;
        var production = trigger.Produces.IsEmpty ? null : new ReactionProductionPreparation(context, trigger.Produces[0], input).Prepare(JavaNames.Type(reaction.Name) + "On" + index + "Append");
        var invocation = trigger.Invokes.IsEmpty ? production?.Invocation : ReactionInvocation.Render(context, trigger.Invokes[0], input);
        if (invocation is null) return null;
        return WithGuard(trigger, new(annotation, input, invocation, null, production));
    }

    internal void Emit(SemanticReactionTrigger model, PreparedReactionTrigger trigger, int index, JavaCodeBuilder body)
    {
        if (trigger.Annotation.Length > 0) body.Line(trigger.Annotation);
        var parameters = trigger.Input.JavaType.Length == 0 ? string.Empty : trigger.Input.JavaType + " input";
        if (trigger.Input.Event is not null) parameters += ", @org.axonframework.messaging.eventhandling.annotation.Timestamp java.time.Instant occurred";
        body.Block($"public void on{index}({parameters})", method =>
        {
            ReactionScheduling.CalendarGuard(method, model);
            if (trigger.Condition is { } condition) method.Line($"if (!({condition})) return;");
            if (model.Kind == SemanticReactionTriggerKind.Interval)
                method.DiffersFromScreenplay("Spring fixed-rate intervals start when this process starts, not at epoch-aligned Screenplay clock boundaries; delivery remains at least once.");
            ReactionInvocation.Dispatch(method, reaction, trigger.Invocation);
        });
        if (model.Kind is SemanticReactionTriggerKind.Interval or SemanticReactionTriggerKind.Schedule)
            context.Degradation.Unsupported("STAGE-AXON-0507", reaction.Id, "Schedules use UTC and process-local Spring scheduling, not a durable scheduler; interval boundaries start with this process.");
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

    bool SupportedEffects(SemanticReactionTrigger trigger) =>
        reaction.From is null && trigger.RequirementId is null &&
        ((trigger.Produces.IsEmpty && trigger.Invokes.Length == 1) || (trigger.Invokes.IsEmpty && trigger.Produces.Length == 1));
}
