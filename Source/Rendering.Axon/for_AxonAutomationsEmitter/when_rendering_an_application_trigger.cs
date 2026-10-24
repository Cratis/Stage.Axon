// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_an_application_trigger : given.a_reaction_model
{
    void Because() => Render();

    [Fact] void should_register_the_reaction() => Code("SubmitOrderReaction").ShouldContain("@org.springframework.stereotype.Component");
    [Fact] void should_wait_for_the_invoked_command() => Code("SubmitOrderReaction").ShouldContain("commands.sendAndWait(new ");
    [Fact] void should_map_the_input_properties() => Code("SubmitOrderReaction").ShouldContain("input.customer()");
    [Fact] void should_disable_effects_during_replay() => Code("SubmitOrderReaction").ShouldContain("@org.axonframework.messaging.eventhandling.replay.annotation.DisallowReplay");
    [Fact] void should_use_a_dedicated_processor() => Code("SubmitOrderReaction").ShouldContain("@org.axonframework.messaging.core.annotation.Namespace(\"reactions\")");
    [Fact] void should_emit_a_system_only_endpoint() => Code("SubmitOrderEndpoint").ShouldContain("@org.springframework.security.access.prepost.PreAuthorize(\"hasRole('SYSTEM')\")");
    [Fact] void should_use_a_kebab_route() => Code("SubmitOrderEndpoint").ShouldContain("/api/triggers/submit-order");
    [Fact] void should_validate_trigger_payloads() => Code("SubmitOrderEndpoint").ShouldContain("@jakarta.validation.Valid @org.springframework.web.bind.annotation.RequestBody SubmitOrder input");
    [Fact] void should_dispatch_to_the_reaction() => Code("SubmitOrderEndpoint").ShouldContain("reaction0.on0(input);");
    [Fact] void should_enable_scheduling() => Code("ReactionConfiguration").ShouldContain("@org.springframework.scheduling.annotation.EnableScheduling");
    [Fact] void should_not_report_unsupported_intent() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(0);
}
