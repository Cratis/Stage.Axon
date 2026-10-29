// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_producing_an_event : given.a_reaction_model
{
    void Establish() => Produce();
    void Because() => Render();

    [Fact] void should_register_the_endpoint() => Code("SubmitOrderEndpoint").ShouldContain("@RestController");
    [Fact] void should_dispatch_an_append_command() => Code("SubmitOrderEndpoint").ShouldContain("commands.sendAndWait(new SubmitOrderOn0Append(input.orderId(), new ");
    [Fact] void should_dispatch_the_append_with_the_http_metadata() => Code("SubmitOrderEndpoint").ShouldContain("Object.class, metadata, null);");
    [Fact] void should_map_event_properties() => Code("SubmitOrderEndpoint").ShouldContain("input.customer()");
    [Fact] void should_identify_the_append_boundary() => Code("SubmitOrderOn0Append").ShouldContain("@TargetEntityId");
    [Fact] void should_not_load_a_stream() => Code("SubmitOrderOn0AppendHandler").ShouldNotContain("Stream");
    [Fact] void should_append_the_produced_event() => Code("SubmitOrderOn0AppendHandler").ShouldContain("appender.append(command.event());");
    [Fact] void should_check_the_event_identity() => Code("SubmitOrderOn0AppendHandler").ShouldContain("command.event().orderId()");
    [Fact] void should_not_emit_a_stream() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/SubmitOrderOn0AppendStream.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_report_degradation() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(0);
}
