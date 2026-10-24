// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_a_reaction_specification_has_prior_events : given.a_reaction_model
{
    void Establish()
    {
        Specify();
        specification = specification with { GivenEvents = specification.ThenEvents };
    }

    void Because() => Render();

    [Fact] void should_disable_rather_than_drop_fixtures() => Code("SubmitOrderFiringSpecificationsTest").ShouldContain("@org.junit.jupiter.api.Disabled(\"STAGE-AXON-0504");
    [Fact] void should_keep_the_original_intent() => Code("SubmitOrderFiringSpecificationsTest").ShouldContain("// Screenplay wanted to");
    [Fact] void should_register_the_explicit_refusal() => context.Specifications[specification.Id].DisabledCode.ShouldEqual("STAGE-AXON-0504");
    [Fact] void should_report_the_unsupported_fixture() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(1);
}
