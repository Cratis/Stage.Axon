// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class AxonViewHttpLiveSpecs
{
    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticKeyedQuery query, string modelType, string projector, SemanticSpecificationEvent given)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(query.Name) + "SseTest";
        var code = new JavaCodeBuilder(package).Line($$"""
            @org.springframework.boot.test.context.SpringBootTest(classes = {{types.BasePackage}}.Application.class,
                webEnvironment = org.springframework.boot.test.context.SpringBootTest.WebEnvironment.RANDOM_PORT)
            @org.springframework.test.annotation.DirtiesContext
            class {{name}} {
                @org.springframework.boot.test.web.server.LocalServerPort
                private int port;
                @org.springframework.beans.factory.annotation.Autowired
                private org.axonframework.common.configuration.Configuration configuration;
                @org.springframework.beans.factory.annotation.Autowired
                private {{projector}} projector;
                @org.springframework.beans.factory.annotation.Autowired
                @org.springframework.beans.factory.annotation.Qualifier({{JavaCodeBuilder.StringLiteral(modelType)}})
                private {{types.BasePackage}}.runtime.ReadModelStore<Object, {{modelType}}> store;

                @org.junit.jupiter.api.Test
                void shouldDeliverInitialDataAndOneUpdateOverHttpSse() throws Exception {
                    store.clear();
                    try (var client = java.net.http.HttpClient.newHttpClient();
                         var executor = java.util.concurrent.Executors.newVirtualThreadPerTaskExecutor()) {
                        var request = java.net.http.HttpRequest.newBuilder(java.net.URI.create("http://127.0.0.1:" + port + {{JavaCodeBuilder.StringLiteral(QueryEndpoint.Route(located, query))}}))
                            .header("Accept", "text/event-stream").GET().build();
                        var response = client.sendAsync(request, java.net.http.HttpResponse.BodyHandlers.ofInputStream())
                            .get(10, java.util.concurrent.TimeUnit.SECONDS);
                        try (var stream = response.body();
                             var reader = new java.io.BufferedReader(new java.io.InputStreamReader(stream, java.nio.charset.StandardCharsets.UTF_8))) {
                            org.junit.jupiter.api.Assertions.assertEquals(200, response.statusCode());
                            org.junit.jupiter.api.Assertions.assertTrue(response.headers().firstValue("Content-Type").orElse("").startsWith("text/event-stream"));
                            var mapper = tools.jackson.databind.json.JsonMapper.builder().build();
                            var initial = mapper.readTree(executor.submit(() -> nextData(reader)).get(10, java.util.concurrent.TimeUnit.SECONDS));
                            org.junit.jupiter.api.Assertions.assertTrue(initial.isArray());
                            org.junit.jupiter.api.Assertions.assertEquals(0, initial.size());
                            configuration.getComponent(org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory.class).create()
                                .executeWithResult(processing -> {
                                    projector.on({{AxonViewEvents.Expression(context, types, given)}}, org.axonframework.messaging.queryhandling.QueryUpdateEmitter.forContext(processing));
                                    return java.util.concurrent.CompletableFuture.completedFuture(null);
                                }).get(10, java.util.concurrent.TimeUnit.SECONDS);
                            var changed = mapper.readTree(executor.submit(() -> nextData(reader)).get(10, java.util.concurrent.TimeUnit.SECONDS));
                            org.junit.jupiter.api.Assertions.assertTrue(changed.isArray());
                            org.junit.jupiter.api.Assertions.assertEquals(1, changed.size());
                            org.junit.jupiter.api.Assertions.assertEquals(mapper.readTree(mapper.writeValueAsString(store.all())), changed);
                        }
                    }
                }

                private static String nextData(java.io.BufferedReader reader) throws java.io.IOException {
                    String line;
                    while ((line = reader.readLine()) != null) {
                        if (line.startsWith("data:")) return line.substring(5).stripLeading();
                    }
                    throw new java.io.EOFException("SSE stream ended before data arrived");
                }
            }
            """);
        context.AddJava(package, name, code.Build(), true, query.Id);
    }
}
