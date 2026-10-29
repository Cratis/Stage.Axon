// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_a_schedule_in_strict_mode : given.a_planned_screenplay
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

    private protected override ArtifactRenderProfile? Profile => AxonRendering.CreateProfile(strict: true);

    [Fact] void should_refuse_the_schedule() => plan.Refusals.SelectMany(refusal => refusal.Codes).ShouldContain("STAGE-AXON-0507");
}
