// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_an_application_trigger_specification : given.a_reaction_model
{
    string code;

    void Establish() => Specify();

    void Because()
    {
        Render();
        code = Code("SubmitOrderFiringSpecificationsTest");
    }

    [Fact] void should_use_the_native_application() => code.ShouldContain("@org.springframework.boot.test.context.SpringBootTest");
    [Fact] void should_inject_the_real_reaction() => code.ShouldContain("@org.springframework.beans.factory.annotation.Autowired private SubmitOrderReaction reaction;");
    [Fact] void should_fire_the_typed_application_trigger() => code.ShouldContain("reaction.on0(new sample.triggers.SubmitOrder(");
    [Fact] void should_pin_the_injected_clock() => code.ShouldContain("org.mockito.Mockito.when(clock.instant())");
    [Fact] void should_assert_the_complete_native_append_set() => code.ShouldContain("Assertions.assertThat(actual).containsExactly(new sample.orders.events.OrderPlaced(");
    [Fact] void should_exclude_storage_consistency_markers_from_facts() => code.ShouldContain("org.axonframework.eventsourcing.eventstore.TerminalEventMessage");
    [Fact] void should_isolate_application_state() => code.ShouldContain("@org.springframework.test.annotation.DirtiesContext");
    [Fact] void should_correlate_the_junit_display_name() => code.ShouldContain(specification.Id.ToString());
    [Fact] void should_claim_the_model_specification() => context.Specifications.ContainsKey(specification.Id).ShouldBeTrue();
    [Fact] void should_not_disable_supported_fixtures() => code.ShouldNotContain("@org.junit.jupiter.api.Disabled");
}
