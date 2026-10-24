// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests.when_emitting_round_trips;

public class with_a_modeled_caller : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    SemanticId id;
    string code;

    void Establish()
    {
        context = new(request, new AxonDegradation(request.Profile));
        id = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).First().Id;
        AxonSpecificationsEmitter.Register(context, id, "example.CommandTest", "executes_command");
        AxonHttpTests.Register(context, new(id,
            [new(new("POST", "/orders", "{\"name\":\"quoted\\\"\\nvalue\"}"), new(202)), new(new("GET", "/orders/one"), new(200, "{\"name\":\"result\"}"))],
            new("modeled-caller", ["MANAGER"], true)));
    }

    void Because()
    {
        new AxonSpecificationsEmitter().Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledHttpRoundTripTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_boot_the_application_and_use_real_http_infrastructure() => code.ShouldContain("@SpringBootTest\n@AutoConfigureMockMvc");
    [Fact] void should_isolate_scenarios_from_other_modeled_state() => code.ShouldContain("@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_EACH_TEST_METHOD)");
    [Fact] void should_wait_for_async_endpoints_before_asserting_the_response() => code.ShouldContain("if (result.getRequest().isAsyncStarted()) {\n            result.getAsyncResult(10000);\n            return http.perform(asyncDispatch(result));");
    [Fact] void should_assert_synchronous_endpoints_without_async_dispatch() => code.ShouldContain("return actions;");
    [Fact] void should_use_only_the_explicit_modeled_roles() => code.ShouldContain(".with(user(\"modeled-caller\").roles(\"MANAGER\"))");
    [Fact] void should_execute_the_command_before_the_query() => code.IndexOf("HttpMethod.valueOf(\"POST\")", StringComparison.Ordinal).ShouldBeLessThan(code.IndexOf("HttpMethod.valueOf(\"GET\")", StringComparison.Ordinal));
    [Fact] void should_assert_the_modeled_command_response() => code.ShouldContain(".andExpect(status().is(202));");
    [Fact] void should_assert_strict_query_json() => code.ShouldContain(".andExpect(content().json(\"{\\\"name\\\":\\\"result\\\"}\", JsonCompareMode.STRICT));");
    [Fact] void should_escape_the_payload_as_a_java_string() => code.ShouldContain(".content(\"{\\\"name\\\":\\\"quoted\\\\\\\"\\\\nvalue\\\"}\")");
    [Fact] void should_identify_the_http_coverage_separately() => code.ShouldContain("@DisplayName(\"HTTP " + id + "\")");
    [Fact] void should_preserve_the_primary_specification_registration() => context.Specifications[id].TestClass.ShouldEqual("example.CommandTest");
}
