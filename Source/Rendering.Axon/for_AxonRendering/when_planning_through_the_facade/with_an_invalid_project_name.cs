// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_an_invalid_project_name : given.a_facade_model
{
    ArtifactRenderPlan _plan;

    void Because() => _plan = AxonRendering.Plan(Request(new() { ProjectName = "my app" }));

    [Fact] void should_report_invalid_names() => _plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-NAMES" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error).ShouldBeTrue();
    [Fact] void should_not_succeed() => _plan.Success.ShouldBeFalse();
    [Fact] void should_not_emit_artifacts() => _plan.Artifacts.ShouldBeEmpty();
}
