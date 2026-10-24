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
            class {{name}} {
                @org.junit.jupiter.api.Test
                void shouldDeliverInitialDataAndOneUpdateOverHttpSseAndCloseContextPromptly() throws Exception {
                    var application = new org.springframework.boot.builder.SpringApplicationBuilder({{types.BasePackage}}.Application.class).run("--server.port=0");
                    try {
                        var port = application.getEnvironment().getProperty("local.server.port", Integer.class);
                        var configuration = application.getBean(org.axonframework.common.configuration.Configuration.class);
                        var projector = application.getBean({{projector}}.class);
                        var store = application.getBean({{JavaCodeBuilder.StringLiteral(modelType)}}, {{types.BasePackage}}.runtime.ReadModelStore.class);
                        var streams = application.getBean({{JavaNames.Type(query.Name)}}Streams.class);
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
                                var mapper = application.getBean(tools.jackson.databind.json.JsonMapper.class);
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
                                org.junit.jupiter.api.Assertions.assertEquals(1, streams.activeCount());
                                var closing = System.nanoTime();
                                application.close();
                                var elapsed = java.time.Duration.ofNanos(System.nanoTime() - closing);
                                org.junit.jupiter.api.Assertions.assertTrue(elapsed.compareTo(java.time.Duration.ofSeconds(5)) < 0, "Context close took " + elapsed);
                                org.junit.jupiter.api.Assertions.assertEquals(0, streams.activeCount());
                            }
                        }
                    } finally {
                        application.close();
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
