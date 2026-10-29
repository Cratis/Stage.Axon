// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_emitting_public_checks : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _test;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        AxonHttpTestsEndpoints.Register(context, new("command", "POST", "/api/public/command", null, "permitAll()", [], null));
        AxonHttpTestsEndpoints.Register(context, new("query", "GET", "/api/public/query", null, "permitAll()", [new("id", true)], "many"));
        AxonHttpTestsEndpoints.Register(context, new("stream", "GET", "/api/public/query", "text/event-stream", "permitAll()", [new("id", true)], "many"));
        AxonHttpTestsContract.Emit(context);
        _test = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/HttpContractRoutesTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_emit_a_native_anonymous_public_endpoint_test() => _test.ShouldContain("void public_endpoints_allow_anonymous_callers()");
    [Fact] void should_supply_the_command_body() => _test.ShouldContain("\"/api/public/command\").contentType(MediaType.APPLICATION_JSON).content(\"{}\").with(anonymous())).andExpect(status().isOk())");
    [Fact] void should_supply_query_arguments() => _test.ShouldContain("\"/api/public/query\").param(\"id\", \"x\").with(anonymous())).andExpect(status().isOk())");
    [Fact] void should_request_the_stream_media_type() => _test.ShouldContain(".accept(MediaType.TEXT_EVENT_STREAM).with(anonymous())).andExpect(status().isOk())");
}
