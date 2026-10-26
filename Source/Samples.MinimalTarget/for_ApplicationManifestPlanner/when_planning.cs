// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Conformance;
using Xunit;

namespace Cratis.Stage.Samples.MinimalTarget.for_ApplicationManifestPlanner;

public class when_planning : given.an_application_request
{
    ArtifactRenderPlan plan;
    CoverageReport coverage;
    CoverageReport determinism;

    void Because()
    {
        plan = planner.Plan(request);
        coverage = RenderCoverage.Check(request, plan, new HashSet<string>());
        determinism = RenderDeterminism.Check(planner, request);
    }

    [Fact] void should_succeed() => plan.Success.ShouldBeTrue();
    [Fact] void should_plan_one_artifact() => plan.Artifacts.Length.ShouldEqual(1);
    [Fact] void should_use_a_relative_markdown_path() => plan.Artifacts.Single().RelativePath.ShouldEqual("application.md");
    [Fact] void should_render_the_application_name() => Encoding.UTF8.GetString(plan.Artifacts.Single().Bytes.AsSpan()).ShouldEqual("# Sample\n");
    [Fact] void should_attribute_only_the_application() => plan.Artifacts.Single().Sources.Single().ShouldEqual(request.Model.Application.Id);
    [Fact] void should_cover_the_empty_application() => coverage.IsClean.ShouldBeTrue();
    [Fact] void should_produce_identical_repeated_plans() => determinism.IsClean.ShouldBeTrue();
}
