// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonApproximationsInput;

public class when_the_opt_in_names_an_unknown_code : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    Cratis.Stage.Contracts.Rendering.ArtifactRenderPlan plan;

    void Because() => plan = AxonRendering.PlanFrom(loaded, request.Scope, profile: AxonRendering.AllowApproximations(request.Profile, "STAGE-AXON-9999"));

    [Fact] void should_refuse_the_profile() => plan.Success.ShouldBeFalse();
    [Fact] void should_report_the_profile_code() => plan.Diagnostics.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.Profile).ShouldBeTrue();
}
