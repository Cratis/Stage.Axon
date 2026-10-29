// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_producing_alongside_an_unsupported_trigger : given.a_reaction_model
{
    void Establish()
    {
        Produce();
        reaction = reaction with { Triggers = [reaction.Triggers[0], new(SemanticReactionTriggerKind.Interval) { Every = 60 }] };
    }

    void Because() => Render();

    [Fact] void should_not_register_the_reaction() => Code("SubmitOrderReaction").ShouldNotContain("@Component");
    [Fact] void should_not_emit_the_append_command() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/SubmitOrderOn0Append.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_reference_the_suppressed_append_command() => Code("SubmitOrderReaction").ShouldNotContain("SubmitOrderOn0Append");
    [Fact] void should_leave_the_producing_trigger_as_unsupported_intent() => Code("SubmitOrderReaction").ShouldContain("trigger 1 with its original guards");
}
