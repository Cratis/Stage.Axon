// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_one_construct_has_multiple_degradations : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonDegradation degradation;

    void Because()
    {
        degradation = new(AxonRendering.CreateProfile(strict: true));
        degradation.Unsupported("STAGE-AXON-LIVE", loaded.Model.Application.Id, "Live delivery is unsupported.");
        degradation.Unsupported("STAGE-AXON-AUTHORIZATION", loaded.Model.Application.Id, "Authorization requires implementation.");
        degradation.Unsupported("STAGE-AXON-LIVE", loaded.Model.Application.Id, "A second live limitation.");
    }

    [Fact]
    void should_have_one_refusal_for_the_construct() => degradation.Refusals.Length.ShouldEqual(1);

    [Fact]
    void should_combine_distinct_codes_in_stable_order() =>
        Assert.Equal<string>(["STAGE-AXON-AUTHORIZATION", "STAGE-AXON-LIVE"], degradation.Refusals.Single().Codes);

    [Fact]
    void should_retain_all_diagnostics() => degradation.UnsupportedConstructs.Count.ShouldEqual(3);

    [Fact]
    void should_satisfy_the_refusal_digest_uniqueness_contract() =>
        ArtifactRenderPlan.Create(request, [], [.. degradation.UnsupportedConstructs], [], degradation.Refusals, []).Refusals.Length.ShouldEqual(1);
}
