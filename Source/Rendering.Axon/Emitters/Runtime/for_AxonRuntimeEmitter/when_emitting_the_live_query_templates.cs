// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_live_query_templates : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(_context);
    }

    [Theory]
    [InlineData("LiveSnapshots")]
    [InlineData("LiveStreams")]
    [InlineData("LiveCapacityException")]
    void should_emit_the_runtime_type_in_main(string name) => Find("src/main/java/", name).ShouldNotBeNull();

    [Theory]
    [InlineData("LiveSnapshotsTest")]
    [InlineData("LiveQueryContractTest")]
    void should_emit_the_proof_under_src_test(string name) => Find("src/test/java/", name).ShouldNotBeNull();

    [Theory]
    [InlineData("LiveSnapshots")]
    [InlineData("LiveStreams")]
    [InlineData("LiveCapacityException")]
    [InlineData("LiveSnapshotsTest")]
    [InlineData("LiveQueryContractTest")]
    void should_not_carry_a_license_header(string name) => Text(name).ShouldNotContain("Copyright");

    [Fact] void should_not_dirty_the_shared_context() => Text("LiveQueryContractTest").ShouldNotContain("DirtiesContext");
    [Fact] void should_not_disable_any_proof() => Text("LiveQueryContractTest").ShouldNotContain("@Disabled");
    [Fact] void should_answer_a_full_server_with_a_problem() => Text("ProblemDetailsAdvice").ShouldContain("live-capacity");
    [Fact] void should_keep_the_publisher_overload() => Text("SseBridge").ShouldContain("public static <T> SseEmitter stream(Flow.Publisher<T> publisher)");

    PlannedArtifact? Find(string root, string name) => _context.Artifacts.FirstOrDefault(artifact => artifact.RelativePath.StartsWith(root, StringComparison.Ordinal) && artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal));

    string Text(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
