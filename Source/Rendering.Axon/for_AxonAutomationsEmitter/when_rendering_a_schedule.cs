// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_a_schedule : given.a_planned_screenplay
{
    private protected override string Model => """
        module Counters
          feature Totals
            slice Automation Sweep
              reaction NightlySweep
                every 1 day
                  invokes SweepCounters
              command SweepCounters
                produces CountersSwept
              event CountersSwept
        """;

    [Fact] void should_render_without_opting_in() => plan.Refusals.IsEmpty.ShouldBeTrue();
    [Fact] void should_use_a_spring_scheduled_method() => Source("NightlySweepReaction").ShouldContain("@Scheduled(fixedRateString = \"PT86400S\")");
    [Fact] void should_describe_the_difference_from_screenplay() => Source("NightlySweepReaction").ShouldContain("// Differs from Screenplay:");
    [Fact] void should_warn_about_the_schedule() => plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0507" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
    [Fact] void should_not_define_an_event_processor() => Exists("ReactionsProcessorConfiguration").ShouldBeFalse();
    [Fact] void should_not_define_a_processor_in_the_reaction() => Source("NightlySweepReaction").ShouldNotContain("EventProcessorDefinition");
    [Fact] void should_enable_scheduling_on_the_application() => Source("Application").ShouldContain("@EnableScheduling");
}
