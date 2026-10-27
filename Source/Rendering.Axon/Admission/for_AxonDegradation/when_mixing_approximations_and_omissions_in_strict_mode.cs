// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_mixing_approximations_and_omissions_in_strict_mode : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonDegradation degradation;

    void Because()
    {
        degradation = new(AxonRendering.AllowApproximations(AxonRendering.CreateProfile(strict: true), "STAGE-AXON-0507"));
        degradation.Approximate("STAGE-AXON-0507", loaded.Model.Application.Id, "Process-local schedules.", "run on a clock", "x/triggers/0");
        degradation.Unsupported("STAGE-AXON-0601", loaded.Model.Application.Id, "Captures are not rendered.");
    }

    [Fact] void should_refuse_only_the_omission() => degradation.Refusals.Single().Codes.ShouldContainOnly("STAGE-AXON-0601");
}
