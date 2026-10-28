// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_lowering_schedules : Specification
{
    [Fact] void should_preserve_second_precision() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 3661 }).ShouldContain("cron = \"1 1 1 * * *\", zone = \"UTC\"");
    [Fact] void should_preserve_interval_seconds() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Interval) { Every = 60 }).ShouldContain("fixedRateString = \"PT60S\"");
    [Fact] void should_refuse_zero_intervals() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Interval) { Every = 0 }).ShouldBeNull();
    [Fact] void should_refuse_invalid_clock_times() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 86400 }).ShouldBeNull();
    [Fact] void should_refuse_invalid_weekdays() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 0, OnDayOfWeek = 7 }).ShouldBeNull();
    [Fact] void should_support_startup() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Startup)).ShouldContain("ApplicationReadyEvent.class");
    [Fact] void should_support_shutdown() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Shutdown)).ShouldContain("ContextClosedEvent.class");

    [Fact] void should_put_the_weekday_into_the_cron_expression() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 32400, OnDayOfWeek = 1 }).ShouldContain("cron = \"0 0 9 * * MON\"");
    [Fact] void should_put_sunday_into_the_cron_expression() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 0, OnDayOfWeek = 0 }).ShouldContain("cron = \"0 0 0 * * SUN\"");
    [Fact] void should_put_the_day_of_month_into_the_cron_expression() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 0, OnDayOfMonth = 31 }).ShouldContain("cron = \"0 0 0 31 * *\"");
    [Fact] void should_put_both_calendar_filters_into_the_cron_expression() => ReactionScheduling.Annotation(new(SemanticReactionTriggerKind.Schedule) { At = 0, OnDayOfWeek = 0, OnDayOfMonth = 31 }).ShouldContain("cron = \"0 0 0 31 * SUN\"");
}
