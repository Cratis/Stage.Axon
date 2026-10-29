// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

/// <summary>Emits the per-application test that pins the generated routes, the contract file and the denial behaviour.</summary>
internal static class AxonHttpTestsContract
{
    const string Name = "HttpContractRoutesTest";

    internal static void Emit(AxonEmissionContext context)
    {
        var endpoints = AxonHttpTestsEndpoints.For(context);
        if (endpoints.Count == 0) return;
        var package = context.BasePackage + ".specifications";
        var code = new JavaCodeBuilder(package).Declare(Name);
        foreach (var type in new[] { "java.util.List", "java.util.ArrayList", "java.util.TreeSet", "java.util.stream.Collectors", "java.util.regex.Pattern", "java.nio.charset.StandardCharsets" }) code.Reference(type);
        code.Import("static org.junit.jupiter.api.Assertions.assertEquals")
            .Import("static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.anonymous")
            .Import("static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user")
            .Import("static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.request")
            .Import("static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status")
            .Line($"@{code.Reference("org.springframework.boot.test.context.SpringBootTest")}")
            .Line($"@{code.Reference("org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc")}")
            .Block("class " + Name, body =>
            {
                var list = body.Reference("java.util.List");
                var exception = body.Reference("java.lang.Exception");
                body.Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}").Line($"private {body.Reference("org.springframework.test.web.servlet.MockMvc")} http;")
                    .Line($"@{body.Reference("org.springframework.beans.factory.annotation.Autowired")}").Line($"private {body.Reference("org.springframework.context.ApplicationContext")} application;")
                    .Line();
                EmitRoutes(body, endpoints, list, exception);
                EmitDenials(context, body, endpoints, exception);
                EmitLive(body, list, TriggerEndpoints(context));
            });
        context.AddJava(package, Name, code.Build(), test: true);
    }

    static void EmitRoutes(JavaCodeBuilder body, IReadOnlyList<HttpEndpoint> endpoints, string list, string exception)
    {
        var rows = endpoints.Select(Row).Order(StringComparer.Ordinal).ToArray();
        body.Line($"@{body.Reference("org.junit.jupiter.api.Test")}")
            .Block($"void generated_routes_match_the_live_mappings() throws {exception}", method => method
                .Line($"assertEquals({list}.of({string.Join(", ", rows.Select(JavaCodeBuilder.StringLiteral))}), live());"))
            .Line()
            .Line($"@{body.Reference("org.junit.jupiter.api.Test")}")
            .Block($"void http_contract_file_matches_the_live_mappings() throws {exception}", method => method
                .Line("var pattern = Pattern.compile(" + JavaCodeBuilder.StringLiteral("\"method\":\"([A-Z]+)\",\"path\":\"([^\"]+)\",\"produces\":\\[([^\\]]*)\\]") + ");")
                .Line("var rows = new ArrayList<String>();")
                .Block("try (var stream = getClass().getResourceAsStream(\"/http-contract.json\"))", read => read
                    .Line("var text = new String(stream.readAllBytes(), StandardCharsets.UTF_8);")
                    .Line("var matcher = pattern.matcher(text);")
                    .Block("while (matcher.find())", each => each
                        .Line("var produces = matcher.group(3).replace(\"\\\"\", \"\");")
                        .Line("rows.add(matcher.group(1) + \" \" + matcher.group(2) + (produces.isEmpty() ? \"\" : \" \" + produces));")))
                .Line("rows.sort(null);")
                .Line("assertEquals(live(), rows);"))
            .Line();
    }

    static void EmitDenials(AxonEmissionContext context, JavaCodeBuilder body, IReadOnlyList<HttpEndpoint> endpoints, string exception)
    {
        var checkable = endpoints.Where(endpoint => endpoint.Authorization is { } expression && expression != "permitAll()" && !expression.Contains("not (", StringComparison.Ordinal)).ToArray();
        if (checkable.Length == 0) return;
        body.Line($"@{body.Reference("org.junit.jupiter.api.Test")}")
            .Block($"void protected_endpoints_deny_anonymous_and_unauthorized_callers() throws {exception}", method =>
            {
                foreach (var endpoint in checkable)
                {
                    var call = Call(context, method, endpoint);
                    method.Line($"http.perform({call}.with(anonymous())).andExpect(status().isUnauthorized());");
                    if (DeniesWrongRole(endpoint.Authorization!))
                        method.Line($"http.perform({call}.with(user(\"http-contract\").roles(\"HTTP_CONTRACT_NONE\"))).andExpect(status().isForbidden());");
                }
            })
            .Line();
    }

    static bool DeniesWrongRole(string expression) => expression switch
    {
        "denyAll()" => true,
        _ when expression.StartsWith("hasRole('", StringComparison.Ordinal) => true,
        _ => expression.Contains(".check(authentication)", StringComparison.Ordinal)
    };

    static string Call(AxonEmissionContext context, JavaCodeBuilder code, HttpEndpoint endpoint)
    {
        var call = $"request({code.Reference("org.springframework.http.HttpMethod")}.valueOf({JavaCodeBuilder.StringLiteral(endpoint.Method)}), {JavaCodeBuilder.StringLiteral(endpoint.Path)})";
        foreach (var parameter in endpoint.Parameters) call += $".param({JavaCodeBuilder.StringLiteral(parameter.Name)}, {JavaCodeBuilder.StringLiteral(HttpSecurityExamples.Parameter(context, parameter))})";
        if (endpoint.Kind == "command") call += $".contentType({code.Reference("org.springframework.http.MediaType")}.APPLICATION_JSON).content({JavaCodeBuilder.StringLiteral(HttpSecurityExamples.Body(context, endpoint))})";
        if (endpoint.Kind == "stream") call += $".accept({code.Reference("org.springframework.http.MediaType")}.TEXT_EVENT_STREAM)";
        return call;
    }

    // Application trigger endpoints are internal and are excluded by handler identity, so ordinary routes under a module named Triggers stay covered.
    static string[] TriggerEndpoints(AxonEmissionContext context) => [.. context.Request.Model.Application.Triggers.Select(trigger => context.BasePackage + ".triggers." + JavaNames.Type(trigger.Name) + "Endpoint").Order(StringComparer.Ordinal)];

    static void EmitLive(JavaCodeBuilder body, string list, string[] triggerEndpoints) => body
        .Block($"private {list}<String> live()", method => method
            .Line($"var mappings = application.getBean(\"requestMappingHandlerMapping\", {body.Reference("org.springframework.web.servlet.mvc.method.annotation.RequestMappingHandlerMapping")}.class);")
            .Line($"var excluded = {method.Reference("java.util.Set")}.of({string.Join(", ", triggerEndpoints.Select(JavaCodeBuilder.StringLiteral))});")
            .Line("var rows = new TreeSet<String>();")
            .Block("for (var handler : mappings.getHandlerMethods().entrySet())", each => each
                .Line("var info = handler.getKey();")
                .Line($"if (excluded.contains({each.Reference("org.springframework.util.ClassUtils")}.getUserClass(handler.getValue().getBeanType()).getName())) continue;")
                .Line("var produces = info.getProducesCondition().getProducibleMediaTypes().stream().map(Object::toString).sorted().collect(Collectors.joining(\",\"));")
                .Block("for (var pattern : info.getPathPatternsCondition().getPatternValues())", pattern => pattern
                    .Line("if (!pattern.startsWith(\"/api/\")) continue;")
                    .Line("for (var verb : info.getMethodsCondition().getMethods()) rows.add(verb + \" \" + pattern + (produces.isEmpty() ? \"\" : \" \" + produces));")))
            .Line("return new ArrayList<>(rows);"));

    static string Row(HttpEndpoint endpoint) => endpoint.Method + " " + endpoint.Path + (endpoint.Produces is null ? string.Empty : " " + endpoint.Produces);
}
