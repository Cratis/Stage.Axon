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
        return trigger.RequirementId is not null ? PrepareNative(trigger, annotation, input) : PrepareDeclarative(trigger, index, annotation, input);
    }

    internal string MethodName(SemanticReactionTrigger model, PreparedReactionTrigger trigger, int index) => model.Kind switch
    {
        SemanticReactionTriggerKind.Event => "on" + trigger.Input.JavaType[(trigger.Input.JavaType.LastIndexOf('.') + 1)..],
        SemanticReactionTriggerKind.Startup => "onStartup",
        SemanticReactionTriggerKind.Shutdown => "onShutdown",
        _ => "onSchedule" + (index + 1)
    };

    internal void Emit(SemanticReactionTrigger model, PreparedReactionTrigger trigger, int index, string name, JavaCodeBuilder body)
    {
        if (trigger.Annotation.Length > 0) body.Line(ReactionJavaReferences.Expression(body, trigger.Annotation));
        var parameters = trigger.Input.JavaType.Length == 0 ? string.Empty : ReactionJavaReferences.Expression(body, trigger.Input.JavaType) + " input";
        var handler = trigger.Input.Event is not null;
        if (handler) parameters += $", @{body.Reference("org.axonframework.messaging.eventhandling.annotation.Timestamp")} {body.Reference("java.time.Instant")} occurred, {body.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext")} context";
        body.Block($"public void {name}({parameters})", method =>
        {
            DescribeSchedule(model, index, method);
            Guard(method, trigger);
            Effect(method, trigger, handler);
        });
        if (trigger.NativeBody is not null) context.ConsumedImplementations.Add(model.RequirementId!);
    }

    internal void EmitApplicationEffect(SemanticReactionTrigger model, PreparedReactionTrigger trigger, string name, string inputType, JavaCodeBuilder body)
    {
        body.Block($"private void {name}({inputType} input)", method =>
        {
            Guard(method, trigger);
            Effect(method, trigger, false);
        });
        if (trigger.NativeBody is not null) context.ConsumedImplementations.Add(model.RequirementId!);
    }

    static void Guard(JavaCodeBuilder method, PreparedReactionTrigger trigger)
    {
        if (trigger.Condition is { } condition) method.Line($"if (!({ReactionJavaReferences.Expression(method, condition)})) return;");
    }

    static bool StandaloneNativeEffect(SemanticReactionTrigger trigger) => trigger.Produces.IsEmpty && trigger.Invokes.IsEmpty;

    static bool StandaloneDeclarativeEffect(SemanticReactionTrigger trigger) =>
        (trigger.Produces.IsEmpty && trigger.Invokes.Length == 1) || (trigger.Invokes.IsEmpty && trigger.Produces.Length == 1);

    void DescribeSchedule(SemanticReactionTrigger model, int index, JavaCodeBuilder method)
    {
        if (model.Kind is not (SemanticReactionTriggerKind.Interval or SemanticReactionTriggerKind.Schedule)) return;
        const string reason = "Schedules use UTC and process-local Spring scheduling, not a durable scheduler; interval boundaries start with this process.";
        context.Degradation.Approximate(AxonRefusalCodes.Schedule, reaction.Id, reason, $"run reaction {reaction.Name} trigger {index + 1} on its modeled clock", $"{reaction.Id}/triggers/{index}");
        method.DiffersFromScreenplay(model.Kind == SemanticReactionTriggerKind.Interval
            ? "Spring fixed-rate intervals start when this process starts, not at epoch-aligned Screenplay clock boundaries; delivery remains at least once."
            : "Spring cron schedules run in UTC inside this process and are not durable; firings missed while the process is down are not replayed.");
    }

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

        // An unplaced body is reported once by InlineImplementationDiagnostics with its specific reason, not here.
        if (!InlineJavaBodies.TryResolve(context, InlineBodySlot.ForReaction(trigger.RequirementId, reaction, slice), out var content, out _)) return null;
        return WithGuard(trigger, new(annotation, input, string.Empty, null) { NativeBody = content });
    }

    void Effect(JavaCodeBuilder method, PreparedReactionTrigger trigger, bool handler)
    {
        if (trigger.NativeBody is { } content)
        {
            ReactionInvocation.Dispatch(method, reaction, body => body.RawBody(content));
            return;
        }

        var invocation = ReactionJavaReferences.Expression(method, trigger.Invocation);
        ReactionInvocation.Dispatch(method, reaction, trigger.Production is null ? ReactionInvocation.Statement(invocation, handler) : invocation);
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
