// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Scaffold;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_the_cli_screen_composition_corpus : given.a_screen_composition_model
{
    ArtifactRenderPlan _plan;

    void Because() => _plan = AxonRendering.Plan(Request(false));

    [Fact] void should_load_language_v2() => loaded.Model.LanguageVersion.ShouldEqual(LanguageVersion.V2);
    [Fact] void should_load_semantic_v2() => loaded.Model.SemanticVersion.ShouldEqual(SemanticVersion.V2);
    [Fact] void should_be_measured() => AxonRendering.IsMeasured(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion).ShouldBeTrue();
    [Fact] void should_be_within_the_admitted_ceiling() => AxonRendering.NewestAdmittedSemanticVersion.IsAtLeast(loaded.Model.SemanticVersion).ShouldBeTrue();
    [Fact] void should_succeed() => _plan.Success.ShouldBeTrue();

    [Fact]
    void should_have_only_the_three_known_warnings() => _plan.Diagnostics.Where(diagnostic => diagnostic.Code != "STAGE-AXON-LOCAL-RUNTIME").Select(diagnostic => (diagnostic.Code, diagnostic.Severity))
        .ShouldEqual(Enumerable.Repeat(("STAGE-AXON-0308", ArtifactRenderDiagnosticSeverity.Warning), 3));

    [Fact]
    void should_surface_the_local_runtime_warning_in_the_plan()
    {
        var warnings = _plan.Diagnostics.Where(diagnostic => diagnostic.Code == "STAGE-AXON-LOCAL-RUNTIME").ToArray();
        warnings.Length.ShouldEqual(AxonMavenProject.IsLocalVersion(AxonRendering.Version) ? 1 : 0);
        foreach (var warning in warnings)
        {
            warning.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
            warning.Message.ShouldEqual(AxonMavenProject.LocalBuildWarning(AxonRendering.Version));
        }
    }
}
