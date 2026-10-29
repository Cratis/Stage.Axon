// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_emitting_runtime_echo : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(request, new(request.Profile));
        AxonHttpTests.Emit(context);
    }

    [Fact] void should_not_emit_synthetic_echo_tests() => context.Artifacts.ShouldBeEmpty();
    [Fact] void should_not_claim_a_modeled_specification() => context.Specifications.ShouldBeEmpty();
    [Fact] void should_not_register_a_synthetic_modeled_scenario() => context.HttpScenarios.ShouldBeEmpty();
}
