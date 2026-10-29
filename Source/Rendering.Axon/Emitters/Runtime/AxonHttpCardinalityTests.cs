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
        foreach (var cardinality in new[] { SemanticQueryCardinality.One, SemanticQueryCardinality.ZeroOrOne })
        {
            var selected = ScopeSelection.Select(context.Request)
                .SelectMany(owner => owner.Slice.Queries.Select(query => (Owner: owner, Query: query)))
                .FirstOrDefault(item => item.Query.Cardinality == cardinality && !QueryPerformer.Required(context, item.Query));
            if (selected.Query is null) continue;
            var modelOwner = ScopeSelection.Locate(context.Request.Model.Application).First(owner => owner.Slice.ReadModels.Any(model => model.Id == selected.Query.ReadModel));
            var model = modelOwner.Slice.ReadModels.First(model => model.Id == selected.Query.ReadModel);
            var package = types.Package(modelOwner);
            var name = cardinality == SemanticQueryCardinality.One ? "HttpRequiredQueryContractTest" : "HttpOptionalQueryContractTest";
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
                body.Line("@Autowired private MockMvc http;").Line("@Autowired private JsonMapper mapper;").Line($"@MockitoBean private {repository} repository;");
                if (cardinality == SemanticQueryCardinality.One)
                {
                    body.Line("@Test").Block("void anAbsentRequiredResultIsAMissingResultProblem() throws Exception", test => test
                        .Line("when(repository.findAll(any(Sort.class))).thenReturn(List.of());")
                        .Line("perform().andExpect(status().isNotFound()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))")
                        .Line("    .andExpect(jsonPath(\"$.type\").value(\"urn:cratis:stage-axon:problem:query-result-missing\"));"));
                }
                EmitMultiple(body, json, modelType, entity);
                body.Block("private ResultActions perform() throws Exception", method => method
                    .Line($"var actions = http.perform({request});")
                    .Line("var result = actions.andReturn();")
                    .Block("if (result.getRequest().isAsyncStarted())", pending => pending.Line("result.getAsyncResult(10000);").Line("return http.perform(asyncDispatch(result));"))
                    .Line("return actions;"));
            });
            context.AddJava(AxonRuntimePackage.JavaPackage(context), name, code.Build(), test: true);
        }
    }

    static string Request(AxonEmissionContext context, LocatedSemanticSlice owner, SemanticKeyedQuery query, QueryShape shape)
    {
        var request = "get(" + JavaCodeBuilder.StringLiteral(QueryEndpoint.Route(owner, query)) + ")";
        foreach (var argument in shape.Arguments)
            request += ".param(" + JavaCodeBuilder.StringLiteral(argument.Name) + ", " + JavaCodeBuilder.StringLiteral(HttpSecurityExamples.Parameter(context, new(argument.Name, true, argument.Type))) + ")";
        return request + ".with(user(\"cardinality\").roles(" + string.Join(", ", DemoIdentityRoles.From(context).Select(JavaCodeBuilder.StringLiteral)) + "))";
    }

    static void Imports(JavaCodeBuilder code)
    {
        foreach (var import in new[]
        {
            "org.junit.jupiter.api.Test", "org.springframework.beans.factory.annotation.Autowired", "org.springframework.boot.test.context.SpringBootTest",
            "org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc", "org.springframework.test.context.bean.override.mockito.MockitoBean",
            "org.springframework.test.web.servlet.MockMvc", "org.springframework.test.web.servlet.ResultActions", "tools.jackson.databind.json.JsonMapper",
            "org.springframework.data.domain.Sort", "java.util.List", "org.springframework.http.MediaType"
        })
        {
            code.Import(import);
        }
        foreach (var import in new[]
        {
            "org.mockito.Mockito.mock", "org.mockito.Mockito.when", "org.mockito.ArgumentMatchers.any", "org.hamcrest.Matchers.not", "org.hamcrest.Matchers.containsString",
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
            .Line($"var entity = mock({entity}.class);")
            .Line("when(entity.toModel()).thenReturn(row);")
            .Line("when(repository.findAll(any(Sort.class))).thenReturn(List.of(entity, entity));")
            .Line("perform().andExpect(status().isInternalServerError()).andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))")
            .Line("    .andExpect(jsonPath(\"$.type\").value(\"urn:cratis:stage-axon:problem:query-cardinality\"))")
            .Line("    .andExpect(jsonPath(\"$.count\").value(2)).andExpect(jsonPath(\"$.rows\").doesNotExist())")
            .Line("    .andExpect(content().string(not(containsString(mapper.writeValueAsString(row)))))")
            .Line("    .andExpect(content().string(not(containsString(\"http-contract\"))));"));
}
