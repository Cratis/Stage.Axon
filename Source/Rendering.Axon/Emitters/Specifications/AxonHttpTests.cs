// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal static class AxonHttpTests
{
    internal static void Register(AxonEmissionContext context, AxonHttpScenario scenario)
    {
        if (scenario.Exchanges.IsDefaultOrEmpty)
        {
            context.Degradation.Unsupported("STAGE-AXON-HTTP-SPEC", scenario.Id, "An HTTP scenario requires at least one request and expected response.");
            return;
        }

        context.HttpScenarios.Add(scenario.Id, scenario);
    }

    internal static void Emit(AxonEmissionContext context)
    {
        EmitRuntimeEcho(context);
        if (context.HttpScenarios.Count == 0) return;
        var package = context.BasePackage + ".specifications";
        const string name = "ModeledHttpRoundTripTest";
        var code = TestClass(Imports(package), name, context.HttpScenarios.Values);
        context.AddJava(package, name, code.Build(), test: true, sources: [.. context.HttpScenarios.Keys]);
    }

    static void EmitRuntimeEcho(AxonEmissionContext context)
    {
        var package = context.BasePackage + ".runtime";
        const string name = "RuntimeHttpExchangeTest";
        const string payload = "{\"id\":\"00000000-0000-0000-0000-000000000001\"}";
        var scenario = new AxonHttpScenario(
            context.Request.Model.Application.Id,
            [new(new("POST", "/_runtime/echo", payload), new(200, payload)), new(new("GET", "/_runtime/echo-async"), new(200, payload))],
            new(string.Empty, [], false));
        var code = Imports(package).Import("org.springframework.context.annotation.Import")
            .Line("@Import(RuntimeHttpRoundTripTest.EchoEndpoint.class)");
        context.AddJava(package, name, TestClass(code, name, [scenario]).Build(), test: true);
    }

    static JavaCodeBuilder TestClass(JavaCodeBuilder code, string name, IEnumerable<AxonHttpScenario> scenarios) => code
            .Line("@SpringBootTest")
            .Line("@AutoConfigureMockMvc")
            .Line("@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_EACH_TEST_METHOD)")
            .Block("class " + name, body =>
            {
                body.Line("@Autowired").Line("private MockMvc http;");
                var index = 0;
                foreach (var scenario in scenarios.OrderBy(scenario => scenario.Id.ToString(), StringComparer.Ordinal))
                {
                    body.Line().Line("@Test")
                        .Line("@DisplayName(" + JavaCodeBuilder.StringLiteral("HTTP " + scenario.Id) + ")")
                        .Block("void scenario_" + index++.ToString(CultureInfo.InvariantCulture) + "() throws Exception", test =>
                        {
                            foreach (var exchange in scenario.Exchanges) EmitExchange(test, exchange, scenario.Caller);
                        });
                }

                EmitPerform(body);
            });

    static void EmitExchange(JavaCodeBuilder code, AxonHttpExchange exchange, AxonHttpCaller caller)
    {
        var request = exchange.Request;
        var builder = "request(HttpMethod.valueOf(" + JavaCodeBuilder.StringLiteral(request.Method) + "), " + JavaCodeBuilder.StringLiteral(request.Path) + ")";
        builder += ".with(" + Caller(caller) + ")";
        if (request.JsonBody is not null) builder += ".contentType(MediaType.APPLICATION_JSON).content(" + JavaCodeBuilder.StringLiteral(request.JsonBody) + ")";
        code.Line("perform(" + builder + ")")
            .Line("    .andExpect(status().is(" + exchange.Response.Status.ToString(CultureInfo.InvariantCulture) + "))" +
                (exchange.Response.ExpectedJson is null ? ";" : string.Empty));
        if (exchange.Response.ExpectedJson is not null)
        {
            code.Line("    .andExpect(content().json(" + JavaCodeBuilder.StringLiteral(exchange.Response.ExpectedJson) + ", JsonCompareMode.STRICT));");
        }
    }

    static void EmitPerform(JavaCodeBuilder code) => code.Line()
        .Block("private ResultActions perform(MockHttpServletRequestBuilder request) throws Exception", body => body
            .Line("var actions = http.perform(request);")
            .Line("var result = actions.andReturn();")
            .Block("if (result.getRequest().isAsyncStarted())", pending => pending
                .Line("result.getAsyncResult(10000);")
                .Line("return http.perform(asyncDispatch(result));"))
            .Line("return actions;"));

    static string Caller(AxonHttpCaller caller) => caller.Authenticated
        ? "user(" + JavaCodeBuilder.StringLiteral(caller.Identity) + ").roles(" + string.Join(", ", caller.Roles.Select(JavaCodeBuilder.StringLiteral)) + ")"
        : "anonymous()";

    static JavaCodeBuilder Imports(string package) => new JavaCodeBuilder(package)
        .Import("org.junit.jupiter.api.DisplayName")
        .Import("org.junit.jupiter.api.Test")
        .Import("org.springframework.beans.factory.annotation.Autowired")
        .Import("org.springframework.boot.test.context.SpringBootTest")
        .Import("org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc")
        .Import("org.springframework.http.HttpMethod")
        .Import("org.springframework.http.MediaType")
        .Import("org.springframework.test.annotation.DirtiesContext")
        .Import("org.springframework.test.json.JsonCompareMode")
        .Import("org.springframework.test.web.servlet.MockMvc")
        .Import("org.springframework.test.web.servlet.ResultActions")
        .Import("org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder")
        .Import("static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.anonymous")
        .Import("static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user")
        .Import("static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch")
        .Import("static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.request")
        .Import("static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content")
        .Import("static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status");
}
