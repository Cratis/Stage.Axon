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
    [InlineData("LiveQueryContractTest")]
    void should_emit_the_proof_under_src_test(string name) => Find("src/test/java/", name).ShouldNotBeNull();

    [Theory]
    [InlineData("LiveQueryContractTest")]
    void should_not_carry_a_license_header(string name) => Text(name).ShouldNotContain("Copyright");

    [Fact] void should_not_dirty_the_shared_context() => Text("LiveQueryContractTest").ShouldNotContain("DirtiesContext");
    [Fact] void should_not_disable_any_proof() => Text("LiveQueryContractTest").ShouldNotContain("@Disabled");
    [Fact] void should_stress_streams_whose_snapshot_is_ready_at_once() => Text("LiveQueryContractTest").ShouldContain("attempt < 200");
    [Fact] void should_fail_the_stress_proof_on_a_logged_server_error() => Text("LiveQueryContractTest").ShouldContain("output.getAll().contains(\"NullPointerException\")");
    [Fact] void should_prove_the_heartbeat_apart_from_the_opening_comment() => Text("LiveQueryContractTest").ShouldContain("!line.equals(\":heartbeat\")");

    PlannedArtifact? Find(string root, string name) => _context.Artifacts.FirstOrDefault(artifact => artifact.RelativePath.StartsWith(root, StringComparison.Ordinal) && artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal));

    string Text(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
