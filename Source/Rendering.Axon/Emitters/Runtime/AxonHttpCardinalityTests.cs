// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime;

internal static class AxonHttpCardinalityTests
{
    internal static void Emit(AxonEmissionContext context)
    {
        var types = new ViewJavaTypes(context.Request.Model.Application, context.BasePackage);
        var selected = ScopeSelection.Select(context.Request)
            .SelectMany(owner => owner.Slice.Queries.Select(query => (Owner: owner, Query: query)))
            .FirstOrDefault(item => Supported(context, item.Query));
        if (selected.Query is null) return;
        var modelOwner = ScopeSelection.Locate(context.Request.Model.Application).First(owner => owner.Slice.ReadModels.Any(model => model.Id == selected.Query.ReadModel));
        var model = modelOwner.Slice.ReadModels.First(model => model.Id == selected.Query.ReadModel);
        var package = types.Package(modelOwner);
        const string name = "HttpOptionalQueryContractTest";
        var code = new JavaCodeBuilder(AxonRuntimePackage.JavaPackage(context)).Declare(name);
        var repository = code.Reference(package + "." + ReadModelEntities.RepositoryName(model));
        var entity = code.Reference(package + "." + ReadModelEntities.EntityName(model));
        var modelType = code.Reference(package + "." + JavaNames.Type(model.Name));
        var values = model.Properties.ToDictionary(property => JavaNames.Member(property.Name), property => HttpSecurityExamples.Value(context, property.Type, 0));
        var json = JavaCodeBuilder.StringLiteral(JsonSerializer.Serialize(values));
        var request = Request(context, selected.Owner, selected.Query, new(selected.Query, model, types));
        Imports(code);
        code.Line("@SpringBootTest").Line("@AutoConfigureMockMvc").Block("class " + name, body =>
        {
            body.Line("@Autowired private MockMvc http;").Line("@Autowired private JsonMapper mapper;").Line($"@Autowired private {repository} repository;")
                .Line("@BeforeEach").Line("@AfterEach").Block("void clearRows()", method => method.Line("repository.deleteAll();"))
                .Line("@Test").Block("void anAbsentOptionalResultIsAnEmptyNotFound() throws Exception", test => test
                    .Line("perform().andExpect(status().isNotFound()).andExpect(content().string(\"\"));"));
            EmitMultiple(body, json, modelType, entity);
            body.Block("private ResultActions perform() throws Exception", method => method
                .Line($"var actions = http.perform({request});")
                .Line("var result = actions.andReturn();")
                .Block("if (result.getRequest().isAsyncStarted())", pending => pending.Line("result.getAsyncResult(10000);").Line("return http.perform(asyncDispatch(result));"))
                .Line("return actions;"));
        });
        context.AddJava(AxonRuntimePackage.JavaPackage(context), name, code.Build(), test: true);
    }

    static bool Supported(AxonEmissionContext context, SemanticKeyedQuery query) =>
        query.Cardinality == SemanticQueryCardinality.ZeroOrOne && !QueryPerformer.Required(context, query) &&
        RoleCallerSupported(context, query.Authorization) &&
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.ReadModels)
            .Any(model => model.Id == query.ReadModel && !ReadModelKeys.IsComposite(context.Request.Model.Application, model));

    // The generic caller holds every modeled role, so it cannot satisfy negated roles or custom claims.
    static bool RoleCallerSupported(AxonEmissionContext context, SemanticAuthorization? authorization) => authorization switch
    {
        null => true,
        SemanticPolicyReference reference => context.Request.Model.Application.Policies
            .Any(policy => policy.Name == reference.Name && RoleCallerSupported(policy.Condition)),
        SemanticLogicalAuthorization logical => RoleCallerSupported(context, logical.Left) && RoleCallerSupported(context, logical.Right),
        _ => false
    };

    static bool RoleCallerSupported(SemanticPolicyCondition condition) => condition switch
    {
        SemanticAnonymousCondition or SemanticAuthenticatedCondition or SemanticRoleCondition => true,
        SemanticLogicalPolicyCondition logical => RoleCallerSupported(logical.Left) && RoleCallerSupported(logical.Right),
        _ => false
    };

    static string Request(AxonEmissionContext context, LocatedSemanticSlice owner, SemanticKeyedQuery query, QueryShape shape)
    {
        var request = "get(" + JavaCodeBuilder.StringLiteral(QueryEndpoint.Route(owner, query)) + ")";
        foreach (var argument in shape.Arguments)
            request += ".param(" + JavaCodeBuilder.StringLiteral(argument.Name) + ", " + JavaCodeBuilder.StringLiteral(HttpSecurityExamples.Parameter(context, new(argument.Name, true, argument.Type))) + ")";
        return request + ".with(user(\"alice\").roles(" + string.Join(", ", DemoIdentityRoles.From(context).Select(JavaCodeBuilder.StringLiteral)) + "))";
    }

    static void Imports(JavaCodeBuilder code)
    {
        foreach (var import in new[]
        {
            "org.junit.jupiter.api.Test", "org.junit.jupiter.api.BeforeEach", "org.junit.jupiter.api.AfterEach",
            "org.springframework.beans.factory.annotation.Autowired", "org.springframework.boot.test.context.SpringBootTest",
            "org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc",
            "org.springframework.test.web.servlet.MockMvc", "org.springframework.test.web.servlet.ResultActions", "tools.jackson.databind.json.JsonMapper",
            "java.util.List", "org.springframework.http.MediaType"
        })
        {
            code.Import(import);
        }
        foreach (var import in new[]
        {
            "org.hamcrest.Matchers.not", "org.hamcrest.Matchers.containsString",
            "org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.user",
            "org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get", "org.springframework.test.web.servlet.request.MockMvcRequestBuilders.asyncDispatch",
            "org.springframework.test.web.servlet.result.MockMvcResultMatchers.content", "org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath",
            "org.springframework.test.web.servlet.result.MockMvcResultMatchers.status"
        })
        {
            code.Import("static " + import);
        }
    }

    static void EmitMultiple(JavaCodeBuilder body, string json, string modelType, string entity) =>
        body.Line("@Test").Block("void multipleRowsAreACardinalityProblemWithoutLeakingValues() throws Exception", test => test
            .Line($"var row = mapper.readValue({json}, {modelType}.class);")
            .Line($"repository.saveAll(List.of(new {entity}(\"cardinality-row-1\", row), new {entity}(\"cardinality-row-2\", row)));")
            .Line("perform().andExpect(status().isInternalServerError()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))")
            .Line("    .andExpect(jsonPath(\"$.type\").value(\"urn:cratis:stage-axon:problem:query-cardinality\"))")
            .Line("    .andExpect(jsonPath(\"$.count\").value(2)).andExpect(jsonPath(\"$.rows\").doesNotExist())")
            .Line("    .andExpect(content().string(not(containsString(mapper.writeValueAsString(row)))))")
            .Line("    .andExpect(content().string(not(containsString(\"http-contract\"))));"));
}
