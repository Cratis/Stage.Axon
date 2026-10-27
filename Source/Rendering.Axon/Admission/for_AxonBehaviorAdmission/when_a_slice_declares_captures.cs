// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_a_slice_declares_captures : given.a_model_with_behaviors
{
    SemanticCapture capture;

    void Establish()
    {
        capture = new(SemanticId.Create(SemanticKind.Capture, "Lines"), "Lines", "Id", [], []);
        Change(slice => slice with { Kind = SemanticSliceKind.Translate, Direction = SemanticTranslationDirection.Inbound, Captures = [capture] });
    }

    void Because() => Emit();

    [Fact] void should_report_one_0601_per_capture() => context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Code == "STAGE-AXON-0601").Artifact.ShouldEqual(capture.Id);
    [Fact] void should_describe_the_capture_in_a_package_info_comment() => PackageInfo().Single().ShouldContain("// Screenplay wanted to capture Lines (" + capture.Id + "/capture)");
    [Fact] void should_keep_the_package_info_to_comments() => PackageInfo().Single().Split('\n').Where(line => line.Length > 0).All(line => line.StartsWith("package ", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_warn_in_default_mode() => context.Degradation.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);

    [Fact]
    void should_refuse_in_strict_mode()
    {
        request = request with { Profile = AxonRendering.CreateProfile(strict: true) };
        Emit();
        context.Degradation.Refusals.Single().Codes.ShouldContainOnly("STAGE-AXON-0601");
    }
}
