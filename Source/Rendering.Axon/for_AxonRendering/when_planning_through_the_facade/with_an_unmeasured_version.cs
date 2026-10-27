// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_an_unmeasured_version : given.an_unmeasured_model
{
    ArtifactRenderPlan _plan;

    void Because() => _plan = AxonRendering.Plan(Request(false));

    [Fact] void should_be_unmeasured() => AxonRendering.IsMeasured(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion).ShouldBeFalse();
    [Fact] void should_succeed() => _plan.Success.ShouldBeTrue();
    [Fact] void should_not_have_errors() => _plan.Diagnostics.Any(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error).ShouldBeFalse();
    [Fact] void should_not_refuse() => _plan.Refusals.ShouldBeEmpty();
    [Fact] void should_warn_about_the_unmeasured_version() => _plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-VERSION" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
}
