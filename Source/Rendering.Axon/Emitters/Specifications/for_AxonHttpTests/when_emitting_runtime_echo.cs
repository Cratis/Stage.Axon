// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_emitting_runtime_echo : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string code;

    void Establish() => context = new(request, new AxonDegradation(request.Profile));

    void Because()
    {
        AxonHttpTests.Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RuntimeHttpExchangeTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_register_only_the_test_fixture_endpoint() => code.ShouldContain("@Import(RuntimeHttpRoundTripTest.EchoEndpoint.class)");
    [Fact] void should_post_a_primitive_concept_identity() => code.ShouldContain("request(HttpMethod.valueOf(\"POST\"), \"/_runtime/echo\")");
    [Fact] void should_read_the_echo_through_an_async_endpoint() => code.ShouldContain("request(HttpMethod.valueOf(\"GET\"), \"/_runtime/echo-async\")");
    [Fact] void should_compare_response_json_strictly() => code.ShouldContain("JsonCompareMode.STRICT");
    [Fact] void should_not_claim_a_modeled_specification() => context.Specifications.ShouldBeEmpty();
    [Fact] void should_not_register_a_synthetic_modeled_scenario() => context.HttpScenarios.ShouldBeEmpty();
}
