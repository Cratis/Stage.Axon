// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using NSubstitute;
using Xunit;

namespace Cratis.Stage.Rendering.Conformance.for_RenderDeterminism;

public class when_comparing_repeated_plans : for_RenderCoverage.given.a_render_request
{
    CoverageReport equal;
    CoverageReport bytes;
    CoverageReport paths;
    CoverageReport diagnostics;
    IArtifactRenderPlanner planner;

    void Because()
    {
        var baseline = Plan([PlannedArtifact.CreateText("output.txt", "one")], []);
        equal = Check(baseline, Plan([PlannedArtifact.CreateText("output.txt", "one")], []));
        bytes = Check(baseline, Plan([PlannedArtifact.CreateText("output.txt", "two")], []));
        paths = Check(baseline, Plan([PlannedArtifact.CreateText("other.txt", "one")], []));
        diagnostics = Check(baseline, Plan([PlannedArtifact.CreateText("output.txt", "one")], [new("test", ArtifactRenderDiagnosticSeverity.Warning, "changed", request.Scope.Artifact)]));
    }

    [Fact] void should_accept_equal_plans_from_independent_instances() => equal.IsClean.ShouldBeTrue();
    [Fact] void should_compare_actual_bytes() => bytes.Issues.Single().Code.ShouldEqual(ConformanceCodes.NondeterministicOutput);
    [Fact] void should_compare_paths() => paths.IsClean.ShouldBeFalse();
    [Fact] void should_compare_typed_diagnostics() => diagnostics.IsClean.ShouldBeFalse();
    [Fact] void should_render_twice() => planner.Received(2).Plan(request);

    CoverageReport Check(ArtifactRenderPlan first, ArtifactRenderPlan second)
    {
        planner = Substitute.For<IArtifactRenderPlanner>();
        planner.Plan(request).Returns(first, second);
        return RenderDeterminism.Check(planner, request);
    }
}
