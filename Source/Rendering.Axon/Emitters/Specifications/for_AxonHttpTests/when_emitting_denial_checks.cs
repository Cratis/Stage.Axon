// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_emitting_denial_checks : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _test;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        AxonHttpTestsEndpoints.Register(context, new("command", "POST", "/api/a/do", null, "hasRole('ADMIN')", [], null));
        AxonHttpTestsEndpoints.Register(context, new("query", "GET", "/api/a/get", null, "denyAll()", [new("id", true)], "many"));
        AxonHttpTestsEndpoints.Register(context, new("stream", "GET", "/api/a/get", "text/event-stream", "denyAll()", [new("id", true)], "many"));
        AxonHttpTestsEndpoints.Register(context, new("query", "GET", "/api/a/other", null, "isAuthenticated()", [], "many"));
        AxonHttpTestsContract.Emit(context);
        _test = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/HttpContractRoutesTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_deny_anonymous_command_callers() => _test.ShouldContain("HttpMethod.valueOf(\"POST\"), \"/api/a/do\").contentType(MediaType.APPLICATION_JSON).content(\"{}\").with(anonymous())).andExpect(status().isUnauthorized())");
    [Fact] void should_forbid_callers_without_the_role() => _test.ShouldContain("\"/api/a/do\").contentType(MediaType.APPLICATION_JSON).content(\"{}\").with(user(\"http-contract\").roles(\"HTTP_CONTRACT_NONE\"))).andExpect(status().isForbidden())");
    [Fact] void should_supply_every_key_before_authorization() => _test.ShouldContain("\"/api/a/get\").param(\"id\", \"x\").with(anonymous())");
    [Fact] void should_request_the_event_stream_for_streams() => _test.ShouldContain(".accept(MediaType.TEXT_EVENT_STREAM).with(anonymous())");
    [Fact] void should_skip_other_expressions_with_a_comment() => _test.ShouldContain("// GET /api/a/other: authorization 'isAuthenticated()' is covered by the runtime contract test.");
    [Fact] void should_not_exercise_the_skipped_endpoint() => _test.ShouldNotContain("\"/api/a/other\")");
}
