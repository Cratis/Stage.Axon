// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_approximating_with_opt_in : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonDegradation degradation;
    bool allowed;

    void Because()
    {
        degradation = new(AxonRendering.AllowApproximations(AxonRendering.CreateProfile(strict: true), "STAGE-AXON-0507"));
        allowed = degradation.Approximate("STAGE-AXON-0507", loaded.Model.Application.Id, "Process-local schedules.", "run on a clock", "x/triggers/0");
    }

    [Fact] void should_allow_it() => allowed.ShouldBeTrue();
    [Fact] void should_still_warn_in_strict_mode() => degradation.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_not_refuse() => degradation.Refusals.ShouldBeEmpty();
    [Fact] void should_keep_the_intent() => degradation.Intents.Single().ModelPath.ShouldEqual("x/triggers/0");
}
