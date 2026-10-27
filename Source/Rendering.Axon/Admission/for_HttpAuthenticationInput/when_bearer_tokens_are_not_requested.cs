// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_HttpAuthenticationInput;

public class when_bearer_tokens_are_not_requested : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Fact] void should_not_carry_the_input() => request.Profile.Inputs.Any(input => input.Name == HttpAuthenticationInput.Name).ShouldBeFalse();
    [Fact] void should_not_request_bearer() => HttpAuthenticationInput.Bearer(request.Profile).ShouldBeFalse();
    [Fact] void should_accept_an_absent_input() => HttpAuthenticationInput.TryRead(request.Profile, out _).ShouldBeTrue();
}
