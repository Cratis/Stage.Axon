// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal static class ReactionScheduling
{
    static readonly string[] _weekdays = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    // These generated annotations contain only framework type references and literal schedule parameters.
    internal static string? Annotation(SemanticReactionTrigger trigger) => trigger.Kind switch
    {
        SemanticReactionTriggerKind.Interval => Interval(trigger.Every),
        SemanticReactionTriggerKind.Schedule => Schedule(trigger),
        _ => EventAnnotation(trigger.Kind)
    };

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

    static string? Schedule(SemanticReactionTrigger trigger) => trigger.At is >= 0 and < 86400 && ValidDays(trigger) ? Cron(trigger.At.Value, trigger) : null;

    static bool ValidDays(SemanticReactionTrigger trigger) =>
        (trigger.OnDayOfMonth is null or (>= 1 and <= 31)) && (trigger.OnDayOfWeek is null or (>= 0 and <= 6));

    static string Cron(int seconds, SemanticReactionTrigger trigger)
    {
        var minutes = seconds / 60;
        var day = trigger.OnDayOfMonth?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "*";
        var weekday = trigger.OnDayOfWeek is { } value ? _weekdays[value] : "*";
        return "@org.springframework.scheduling.annotation.Scheduled(cron = " +
            JavaCodeBuilder.StringLiteral($"{seconds % 60} {minutes % 60} {seconds / 3600} {day} * {weekday}") + ", zone = \"UTC\")";
    }
}
