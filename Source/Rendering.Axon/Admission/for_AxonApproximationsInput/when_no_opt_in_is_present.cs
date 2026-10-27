// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonApproximationsInput;

public class when_no_opt_in_is_present : Specification
{
    [Fact] void should_not_add_an_input_to_the_default_profile() => AxonRendering.CreateProfile().Inputs.Any(input => input.Name == AxonApproximationsInput.Name).ShouldBeFalse();
    [Fact] void should_allow_nothing() => new AxonDegradation(AxonRendering.CreateProfile()).AllowedApproximations.ShouldBeEmpty();
}
