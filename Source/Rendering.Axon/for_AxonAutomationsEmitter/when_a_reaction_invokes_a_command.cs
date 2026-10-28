// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_a_reaction_invokes_a_command : given.a_planned_screenplay
{
    private protected override string Model => """
        module Counters
          feature Totals
            slice StateChange Record
              command RecordCount
                id String identifier
                produces CountRecorded
                  id = id
              event CountRecorded
                id String
            slice Automation Reset
              reaction ResetAfterRecording
                when CountRecorded
                  invokes ResetCount
                    id = id
              command ResetCount
                id String identifier
                produces CountReset
                  id = id
              event CountReset
                id String
        """;

    [Fact] void should_plan_without_refusals() => plan.Refusals.IsEmpty.ShouldBeTrue();
    [Fact] void should_be_a_spring_component() => Source("ResetAfterRecordingReaction").ShouldContain("@Component");
    [Fact] void should_handle_the_event_natively() => Source("ResetAfterRecordingReaction").ShouldContain("@EventHandler");
    [Fact] void should_not_replay_the_effects() => Source("ResetAfterRecordingReaction").ShouldContain("@DisallowReplay");
    [Fact] void should_use_its_own_processor_namespace() => Source("ResetAfterRecordingReaction").ShouldContain("@Namespace(\"reactionplan.counters.totals.reset.reactions\")");
    [Fact] void should_name_the_handler_after_the_event() => Source("ResetAfterRecordingReaction").ShouldContain("public void onCountRecorded(CountRecorded input");
    [Fact] void should_receive_the_processing_context() => Source("ResetAfterRecordingReaction").ShouldContain("ProcessingContext context");
    [Fact] void should_send_the_command_in_the_handler_context() => Source("ResetAfterRecordingReaction").ShouldContain("commands.sendAndWait(new ResetCount(input.id()), context);");
    [Fact] void should_not_inject_a_clock() => Source("ResetAfterRecordingReaction").ShouldNotContain("Clock");
    [Fact] void should_not_emit_a_reaction_configuration() => Exists("ReactionConfiguration").ShouldBeFalse();
    [Fact] void should_not_emit_a_reaction_properties_file() => plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith("stage-reactions.properties", StringComparison.Ordinal)).ShouldBeFalse();
}
