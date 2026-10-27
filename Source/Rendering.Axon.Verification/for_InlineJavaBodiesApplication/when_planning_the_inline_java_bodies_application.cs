// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_InlineJavaBodiesApplication;

public class when_planning_the_inline_java_bodies_application : Specification
{
    ArtifactRenderPlan _plan;

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        var compilation = await SemanticModelLoader.LoadAsync(Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus"), ["inline-java-bodies.play"], null, "CorpusInlineJavaBodies");
        compilation.Success.ShouldBeTrue();
        _plan = AxonRendering.PlanFrom(compilation.Loaded!, new(ArtifactRenderScopeKind.Application, compilation.Loaded!.Model.Application.Id), default, CorpusRenderer.Profile);
    }

    [Fact] void should_plan_without_refusals() => (_plan.Success && _plan.Refusals.IsEmpty).ShouldBeTrue();
    [Fact] void should_place_the_command_handler_body() => Source("RecordCountHandler").ShouldContain("// inline-java-bodies: command handler");
    [Fact] void should_place_the_reaction_body() => Source("GuardRecordedCountReaction").ShouldContain("// inline-java-bodies: reaction effect");
    [Fact] void should_place_the_performer_body() => Source("DoubledTotalsPerformer").ShouldContain("// inline-java-bodies: query performer");
    [Fact] void should_place_the_reducer_body() => Source("RebuiltTotalReducer").ShouldContain("// inline-java-bodies: reducer transition");
    [Fact] void should_import_the_produced_event_for_the_handler_body() => Source("RecordCountHandler").ShouldContain("import corpusinlinejavabodies.bodies.events.CountRecorded;");
    [Fact] void should_not_disable_any_generated_test() => _plan.Artifacts.Where(artifact => artifact.RelativePath.Contains("/src/test/", StringComparison.Ordinal)).Any(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).Contains("@Disabled", StringComparison.Ordinal)).ShouldBeFalse();

    [Theory]
    [InlineData("STAGE-AXON-IMPL")]
    [InlineData("STAGE-AXON-IMPL-LANGUAGE")]
    [InlineData("STAGE-AXON-0401")]
    [InlineData("STAGE-AXON-0504")]
    [InlineData("STAGE-AXON-0505")]
    [InlineData("STAGE-AXON-0306")]
    [InlineData("STAGE-AXON-0112")]
    [InlineData("STAGE-AXON-SPEC")]
    public void should_consume_every_body_without_degrading(string code) => _plan.Diagnostics.Any(diagnostic => diagnostic.Code == code).ShouldBeFalse();

    string Source(string name) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
