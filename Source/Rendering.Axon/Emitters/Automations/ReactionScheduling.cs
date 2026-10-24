// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal static class ReactionScheduling
{
    internal static string? Annotation(SemanticReactionTrigger trigger) => trigger.Kind switch
    {
        SemanticReactionTriggerKind.Interval => Interval(trigger.Every),
        SemanticReactionTriggerKind.Schedule => Schedule(trigger),
        _ => EventAnnotation(trigger.Kind)
    };

    internal static void CalendarGuard(JavaCodeBuilder body, SemanticReactionTrigger trigger)
    {
        if (trigger.Kind != SemanticReactionTriggerKind.Schedule) return;
        if (trigger.OnDayOfMonth is null && trigger.OnDayOfWeek is null) return;
        body.Line("var date = java.time.LocalDate.now(clock.withZone(java.time.ZoneOffset.UTC));");
        if (trigger.OnDayOfMonth is { } day) body.Line($"if (date.getDayOfMonth() != {day}) return;");
        if (trigger.OnDayOfWeek is { } weekday) body.Line($"if (date.getDayOfWeek().getValue() % 7 != {weekday}) return;");
    }

    static string? EventAnnotation(SemanticReactionTriggerKind kind) => kind switch
    {
        SemanticReactionTriggerKind.Event => "@org.axonframework.messaging.eventhandling.annotation.EventHandler",
        SemanticReactionTriggerKind.ApplicationTrigger => string.Empty,
        SemanticReactionTriggerKind.Startup => "@org.springframework.context.event.EventListener(org.springframework.boot.context.event.ApplicationReadyEvent.class)",
        SemanticReactionTriggerKind.Shutdown => "@org.springframework.context.event.EventListener(org.springframework.context.event.ContextClosedEvent.class)",
        _ => null
    };

    static string? Interval(long? seconds) => seconds is > 0
        ? "@org.springframework.scheduling.annotation.Scheduled(fixedRateString = " + JavaCodeBuilder.StringLiteral("PT" + seconds + "S") + ")" : null;

    static string? Schedule(SemanticReactionTrigger trigger) => trigger.At is >= 0 and < 86400 && ValidDays(trigger) ? Cron(trigger.At.Value) : null;

    static bool ValidDays(SemanticReactionTrigger trigger) =>
        (trigger.OnDayOfMonth is null or (>= 1 and <= 31)) && (trigger.OnDayOfWeek is null or (>= 0 and <= 6));

    static string Cron(int seconds)
    {
        var minutes = seconds / 60;
        return "@org.springframework.scheduling.annotation.Scheduled(cron = " +
            JavaCodeBuilder.StringLiteral($"{seconds % 60} {minutes % 60} {seconds / 3600} * * *") + ", zone = \"UTC\")";
    }
}
