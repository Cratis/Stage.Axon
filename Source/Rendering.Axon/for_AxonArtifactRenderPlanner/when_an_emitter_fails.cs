// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_an_emitter_fails : given.an_axon_request
{
    ArtifactRenderPlan _plan;

    void Because() => _plan = given.a_failed_emitter.Plan(request);

    [Fact] void should_continue_the_plan() => _plan.Success.ShouldBeTrue();
    [Fact] void should_discard_partial_non_type_artifacts() => _plan.Artifacts.Any(artifact => artifact.RelativePath == "partial.txt").ShouldBeFalse();
    [Fact] void should_emit_the_dependent_type() => Java("Dependent").ShouldContain("allocated.value()");
    [Fact] void should_preserve_allocated_record_signatures() => Java("Allocated").ShouldContain("public record Allocated(String value)");
    [Fact] void should_make_the_record_stub_throw() => Java("Allocated").ShouldContain("throw new java.lang.UnsupportedOperationException");
    [Fact] void should_emit_preallocated_type_names() => Java("Unfinished").ShouldContain("public class Unfinished");
    [Fact] void should_warn_instead_of_refusing() => _plan.Diagnostics.Single(diagnostic => diagnostic.Code == "STAGE-AXON-GENERATION").Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_document_the_failure() => Text("UNSUPPORTED.md").ShouldContain("allocated types are unsupported stubs");
    [Fact] void should_produce_repeatable_output() => string.Join('\n', _plan.Artifacts.Select(artifact => artifact.RelativePath + Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))).ShouldEqual(string.Join('\n', given.a_failed_emitter.Plan(request).Artifacts.Select(artifact => artifact.RelativePath + Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))));

    string Java(string name) => Text("src/main/java/proof/" + name + ".java");
    string Text(string path) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
