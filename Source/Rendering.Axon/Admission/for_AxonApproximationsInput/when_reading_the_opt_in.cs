// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonApproximationsInput;

public class when_reading_the_opt_in : Specification
{
    Cratis.Stage.Contracts.Rendering.ArtifactRenderProfile once;
    Cratis.Stage.Contracts.Rendering.ArtifactRenderProfile twice;

    void Because()
    {
        once = AxonRendering.AllowApproximations(AxonRendering.CreateProfile(), "STAGE-AXON-0507", "STAGE-AXON-0507");
        twice = AxonRendering.AllowApproximations(once, "STAGE-AXON-0507");
    }

    [Fact] void should_round_trip_the_distinct_codes() => AxonApproximationsInput.TryRead(once, out _).ShouldBeTrue();

    [Fact]
    void should_carry_the_code()
    {
        AxonApproximationsInput.TryRead(once, out var codes);
        codes.ShouldContainOnly("STAGE-AXON-0507");
    }

    [Fact] void should_be_idempotent_when_applied_twice() => twice.Inputs.Count(input => input.Name == AxonApproximationsInput.Name).ShouldEqual(1);
}
