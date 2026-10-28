// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionTriggerEmitter;

public class when_placing_java_bodies_from_a_parsed_model : Specification
{
    const string Model = """
        module Bodies
          feature Counters
            slice StateChange Record
              command RecordCount
                id String identifier
                produces CountRecorded
                  id = id
                produces CountDeleted
                  id = id
              event CountRecorded
                id String
              event CountDeleted
                id String
            slice Automation Guards
              reaction GuardRecorded
                when CountRecorded
                  ```java
                  // reaction one
                  ```
              reaction GuardDeleted
                when CountDeleted
                  ```java
                  // reaction two
                  ```
        """;

    ArtifactRenderPlan _plan;

    async Task Establish()
    {
        var directory = Directory.CreateTempSubdirectory("reaction-bodies");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "model.play"), Model);
            var loaded = await SemanticModelLoader.LoadFromPathAsync(directory.FullName, null, "Reactionbodies");
            _plan = AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact] void should_place_the_first_reaction_body_in_its_own_reaction() => Source("GuardRecordedReaction").ShouldContain("// reaction one");
    [Fact] void should_place_the_second_reaction_body_in_its_own_reaction() => Source("GuardDeletedReaction").ShouldContain("// reaction two");
    [Fact] void should_not_give_a_reaction_the_body_of_its_sibling() => (Source("GuardRecordedReaction").Contains("reaction two", StringComparison.Ordinal) || Source("GuardDeletedReaction").Contains("reaction one", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_report_the_reaction_as_unregistered() => _plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0504").ShouldBeFalse();

    string Source(string name) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
