// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Java;
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

    [Fact]
    void should_require_both_calendar_filters()
    {
        var code = new JavaCodeBuilder("sample");
        ReactionScheduling.CalendarGuard(code, new(SemanticReactionTriggerKind.Schedule) { At = 0, OnDayOfWeek = 0, OnDayOfMonth = 31 });
        code.Build().ShouldContain("date.getDayOfMonth() != 31");
        code.Build().ShouldContain("date.getDayOfWeek().getValue() % 7 != 0");
        code.Build().ShouldContain("clock.withZone(java.time.ZoneOffset.UTC)");
    }
}
