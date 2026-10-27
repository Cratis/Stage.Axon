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
        AxonHttpTestsContract.Emit(context);
        AxonHttpContractJson.Emit(context);
        if (context.HttpScenarios.Count == 0) return;
        var package = context.BasePackage + ".specifications";
        const string name = "ModeledHttpRoundTripTest";
        var scenarios = context.HttpScenarios.Values.OrderBy(scenario => scenario.Id.ToString(), StringComparer.Ordinal).ToArray();
        var fragments = scenarios.SelectMany(scenario => scenario.ArrangeFragments.Concat(scenario.Exchanges.SelectMany(exchange => exchange.Response.AssertionFragments)));
        var code = TestClass(Imports(package, name, fragments), name, scenarios);
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
        var code = Imports(package, name).Declare("RuntimeHttpRoundTripTest");
        code.Line($"@{code.Reference("org.springframework.context.annotation.Import")}(RuntimeHttpRoundTripTest.EchoEndpoint.class)");
        context.AddJava(package, name, TestClass(code, name, [scenario]).Build(), test: true);
    }

    static JavaCodeBuilder TestClass(JavaCodeBuilder code, string name, IEnumerable<AxonHttpScenario> scenarios) => code
            .Line($"@{code.Reference("org.springframework.boot.test.context.SpringBootTest")}")
            .Line($"@{code.Reference("org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc")}")
            .Line($"@{code.Reference("org.springframework.test.annotation.DirtiesContext")}(classMode = {code.Reference("org.springframework.test.annotation.DirtiesContext")}.ClassMode.AFTER_EACH_TEST_METHOD)")
            .Block("class " + name, body =>
            {
                body.Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}").Line($"private {body.Reference("org.springframework.test.web.servlet.MockMvc")} http;")
                    .Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}").Line($"private {body.Reference("org.springframework.context.ApplicationContext")} application;");
                var index = 0;
                foreach (var scenario in scenarios.OrderBy(scenario => scenario.Id.ToString(), StringComparer.Ordinal))
                {
                    body.Line().Line($"@{body.Reference("org.junit.jupiter.api.Test")}")
                        .Line($"@{body.Reference("org.junit.jupiter.api.DisplayName")}(" + JavaCodeBuilder.StringLiteral("HTTP " + scenario.Id) + ")")
                        .Block("void scenario_" + index++.ToString(CultureInfo.InvariantCulture) + $"() throws {body.Reference("java.lang.Exception")}", test =>
                        {
                            foreach (var arrange in scenario.Arrange) test.Line(arrange);
                            foreach (var fragment in scenario.ArrangeFragments) test.Line(fragment.Render(test));
                            foreach (var exchange in scenario.Exchanges) EmitExchange(test, exchange, scenario.Caller);
                        });
                }

                EmitPerform(body);
            });

    static void EmitExchange(JavaCodeBuilder code, AxonHttpExchange exchange, AxonHttpCaller caller)
    {
        var request = exchange.Request;
        var builder = $"request({code.Reference("org.springframework.http.HttpMethod")}.valueOf(" + JavaCodeBuilder.StringLiteral(request.Method) + "), " + JavaCodeBuilder.StringLiteral(request.Path) + ")";
        builder += ".with(" + Caller(code, caller) + ")";
        if (request.JsonBody is not null) builder += $".contentType({code.Reference("org.springframework.http.MediaType")}.APPLICATION_JSON).content(" + JavaCodeBuilder.StringLiteral(request.JsonBody) + ")";
        var assertions = new List<string> { "status().is(" + exchange.Response.Status.ToString(CultureInfo.InvariantCulture) + ")" };
        if (exchange.Response.ExpectedJson is not null)
            assertions.Add("content().json(" + JavaCodeBuilder.StringLiteral(exchange.Response.ExpectedJson) + $", {code.Reference("org.springframework.test.json.JsonCompareMode")}.STRICT)");
        assertions.AddRange(exchange.Response.Assertions);
        assertions.AddRange(exchange.Response.AssertionFragments.Select(fragment => fragment.Render(code)));
        if (assertions.Exists(assertion => assertion.Contains("content()", StringComparison.Ordinal)))
            code.Import("static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content");
        code.Line("perform(" + builder + ")");
        for (var index = 0; index < assertions.Count; index++)
            code.Line("    .andExpect(" + assertions[index] + ")" + (index == assertions.Count - 1 ? ";" : string.Empty));
    }

    static void EmitPerform(JavaCodeBuilder code) => code.Line()
        .Block($"private {code.Reference("org.springframework.test.web.servlet.ResultActions")} perform({code.Reference("org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder")} request) throws {code.Reference("java.lang.Exception")}", body => body
            .Line("var actions = http.perform(request);")
            .Line("var result = actions.andReturn();")
            .Block("if (result.getRequest().isAsyncStarted())", pending => pending
                .Line("result.getAsyncResult(10000);")
                .Line("return http.perform(asyncDispatch(result));"))
            .Line("return actions;"));

    static string Caller(JavaCodeBuilder code, AxonHttpCaller caller)
    {
        var method = caller.Authenticated ? "user" : "anonymous";
        code.Import("static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors." + method);
        return caller.Authenticated
            ? "user(" + JavaCodeBuilder.StringLiteral(caller.Identity) + ").roles(" + string.Join(", ", caller.Roles.Select(JavaCodeBuilder.StringLiteral)) + ")"
            : "anonymous()";
    }

    static JavaCodeBuilder Imports(string package, string name, IEnumerable<Fragment>? fragments = null)
    {
        var code = new JavaCodeBuilder(package).Declare(name);
        foreach (var fragment in fragments ?? []) fragment.Claim?.Invoke(code);
        foreach (var type in new[]
        {
            "org.junit.jupiter.api.DisplayName", "org.junit.jupiter.api.Test",
            "org.springframework.beans.factory.annotation.Autowired", "org.springframework.boot.test.context.SpringBootTest",
            "org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc", "org.springframework.http.HttpMethod",
            "org.springframework.test.annotation.DirtiesContext", "org.springframework.test.web.servlet.MockMvc",
            "org.springframework.test.web.servlet.ResultActions", "org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder"
        })
        {
            code.Reference(type);
        }
        return code.Import("static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch")
            .Import("static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.request")
            .Import("static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status");
    }

    internal sealed record Fragment(Func<JavaCodeBuilder, string> Render, Action<JavaCodeBuilder>? Claim = null);
}
