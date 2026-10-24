// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_producing_an_event : given.a_reaction_model
{
    void Establish() => Produce();
    void Because() => Render();

    [Fact] void should_register_the_reaction() => Code("SubmitOrderReaction").ShouldContain("@org.springframework.stereotype.Component");
    [Fact] void should_dispatch_an_append_command() => Code("SubmitOrderReaction").ShouldContain("commands.sendAndWait(new SubmitOrderOn0Append(input.orderId(), new ");
    [Fact] void should_map_event_properties() => Code("SubmitOrderReaction").ShouldContain("input.customer()");
    [Fact] void should_identify_the_append_boundary() => Code("SubmitOrderOn0Append").ShouldContain("@org.axonframework.modelling.annotation.TargetEntityId");
    [Fact] void should_load_the_current_stream_position() => Code("SubmitOrderOn0AppendHandler").ShouldContain("@org.axonframework.modelling.annotation.InjectEntity(idProperty = \"eventSourceId\")");
    [Fact] void should_append_the_produced_event() => Code("SubmitOrderOn0AppendHandler").ShouldContain("appender.append(command.event());");
    [Fact] void should_check_the_event_identity() => Code("SubmitOrderOn0AppendHandler").ShouldContain("command.event().orderId()");
    [Fact] void should_use_the_shared_primary_tag() => Code("SubmitOrderOn0AppendStream").ShouldContain("tagKey = \"orderId\"");
    [Fact] void should_preserve_position_for_other_stream_events() => Code("SubmitOrderOn0AppendStream").ShouldContain("OrderMoved event");
    [Fact] void should_not_report_degradation() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(0);
}
