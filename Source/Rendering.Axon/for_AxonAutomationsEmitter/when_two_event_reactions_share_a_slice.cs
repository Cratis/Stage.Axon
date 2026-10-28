// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_two_event_reactions_share_a_slice : given.a_planned_screenplay
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
              reaction ResetAgain
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

    [Fact] void should_emit_one_processor_configuration() => plan.Artifacts.Count(artifact => artifact.RelativePath.EndsWith("/ReactionsProcessorConfiguration.java", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_define_the_namespace_processor_once() => Source("ReactionsProcessorConfiguration").Split("pooledStreamingMatching(").Length.ShouldEqual(2);
    [Fact] void should_start_at_the_latest_token() => Source("ReactionsProcessorConfiguration").ShouldContain("config.initialToken(source -> source.latestToken(null))");
    [Fact] void should_not_define_a_processor_in_either_reaction() => (Source("ResetAfterRecordingReaction") + Source("ResetAgainReaction")).ShouldNotContain("EventProcessorDefinition");
}
