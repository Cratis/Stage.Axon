// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class in_strict_mode_with_an_unmeasured_version : given.an_unmeasured_model
{
    ArtifactRenderPlan _plan;

    void Because() => _plan = AxonRendering.Plan(Request(true));

    [Fact] void should_not_succeed() => _plan.Success.ShouldBeFalse();
    [Fact] void should_refuse() => _plan.Refusals.ShouldNotBeEmpty();
    [Fact] void should_report_the_version_as_an_error() => _plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-VERSION" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error).ShouldBeTrue();
}
