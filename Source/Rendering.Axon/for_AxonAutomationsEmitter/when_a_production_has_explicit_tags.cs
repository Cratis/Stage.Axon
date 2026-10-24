// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_a_production_has_explicit_tags : given.a_reaction_model
{
    void Establish()
    {
        Produce();
        var trigger = reaction.Triggers[0];
        reaction = reaction with { Triggers = [trigger with { Produces = [trigger.Produces[0] with { Tags = ["priority"] }] }] };
    }

    void Because() => Render();

    [Fact] void should_not_register_partial_effects() => Code("SubmitOrderReaction").ShouldNotContain("@org.springframework.stereotype.Component");
    [Fact] void should_leave_the_original_intent() => Code("SubmitOrderReaction").ShouldContain("// Screenplay wanted to");
    [Fact] void should_report_the_unsupported_trigger() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(1);
    [Fact] void should_not_emit_an_append_handler() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("AppendHandler.java", StringComparison.Ordinal)).ShouldBeFalse();
}
