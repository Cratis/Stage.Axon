// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_approximating_without_opt_in : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonDegradation degradation;
    AxonDegradation strict;
    bool allowed;
    bool strictAllowed;

    void Because()
    {
        degradation = new(request.Profile);
        allowed = degradation.Approximate("STAGE-AXON-0507", loaded.Model.Application.Id, "Process-local schedules.", "run on a clock", "x/triggers/0");
        strict = new(AxonRendering.CreateProfile(strict: true));
        strictAllowed = strict.Approximate("STAGE-AXON-0507", loaded.Model.Application.Id, "Process-local schedules.", "run on a clock", "x/triggers/0");
    }

    [Fact] void should_not_allow_it() => allowed.ShouldBeFalse();
    [Fact] void should_record_a_warning() => degradation.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_record_the_intent() => degradation.Intents.Single().Intent.ShouldEqual("run on a clock");
    [Fact] void should_name_the_opt_in() => degradation.UnsupportedConstructs.Single().Message.ShouldContain("AxonRendering.AllowApproximations");
    [Fact] void should_not_allow_it_in_strict_mode() => strictAllowed.ShouldBeFalse();
    [Fact] void should_record_an_error_in_strict_mode() => strict.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Error);
    [Fact] void should_refuse_in_strict_mode() => strict.Refusals.Single().Codes.ShouldContainOnly("STAGE-AXON-0507");
}
