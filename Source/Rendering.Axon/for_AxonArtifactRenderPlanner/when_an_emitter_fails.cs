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
    [Fact] void should_preserve_completed_non_type_artifacts() => Text("completed.txt").ShouldEqual("completed artifact");
    [Fact] void should_emit_the_dependent_type() => Java("Dependent").ShouldContain("allocated.value()");
    [Fact] void should_preserve_the_completed_concept_and_factory() => Java("Allocated").ShouldContain(given.a_failed_emitter.Concept);
    [Fact] void should_preserve_the_completed_enum_and_its_conversions() => Java("Status").ShouldContain(given.a_failed_emitter.Enumeration);
    [Fact] void should_use_the_explicit_incomplete_type_api() => Java("Unfinished").ShouldContain(given.a_failed_emitter.Incomplete);
    [Fact] void should_warn_instead_of_refusing() => _plan.Diagnostics.Single(diagnostic => diagnostic.Code == "STAGE-AXON-GENERATION").Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_document_the_failure() => Text("UNSUPPORTED.md").ShouldContain("completed artifacts are retained");
    [Fact] void should_not_refuse_in_default_mode() => _plan.Refusals.ShouldBeEmpty();

    [Fact]
    void should_refuse_the_emitter_failure_in_strict_mode()
    {
        var plan = given.a_failed_emitter.Plan(request with { Profile = AxonRendering.CreateProfile(strict: true) });
        plan.Success.ShouldBeFalse();
        plan.Artifacts.ShouldBeEmpty();
        plan.Diagnostics.Single(diagnostic => diagnostic.Code == "STAGE-AXON-GENERATION").Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Error);
        plan.Refusals.Any(refusal => refusal.Codes.Contains("STAGE-AXON-GENERATION")).ShouldBeTrue();
    }

    [Fact] void should_produce_repeatable_output() => string.Join('\n', _plan.Artifacts.Select(artifact => artifact.RelativePath + Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))).ShouldEqual(string.Join('\n', given.a_failed_emitter.Plan(request).Artifacts.Select(artifact => artifact.RelativePath + Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))));

    string Java(string name) => Text("src/main/java/proof/" + name + ".java");
    string Text(string path) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
