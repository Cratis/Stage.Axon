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
    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticKeyedQuery query, string modelType, string projector, Func<JavaCodeBuilder, string> eventArguments)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(query.Name) + "SseTest";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(JavaNames.Type(query.Name)).Declare(JavaNames.Type(query.Name) + "Streams")
            .Import(types.BasePackage + ".Application");
        foreach (var model in located.Slice.ReadModels) code.Declare(JavaNames.Type(model.Name));
        var projectorName = projector[(projector.LastIndexOf('.') + 1)..];
        if (projector[..projector.LastIndexOf('.')] == package) code.Declare(projectorName);
        else code.Import(projector);
        var storeType = code.Reference(types.BasePackage + ".runtime.ReadModelStore");
        code.Line($$"""
            class {{name}} {
                @{{code.Reference("org.junit.jupiter.api.Test")}}
                void shouldDeliverInitialDataAndOneUpdateOverHttpSseAndCloseContextPromptly() throws Exception {
                    var application = new {{code.Reference("org.springframework.boot.builder.SpringApplicationBuilder")}}(Application.class).run("--server.port=0");
                    try {
                        var port = application.getEnvironment().getProperty("local.server.port", Integer.class);
                        var configuration = application.getBean({{code.Reference("org.axonframework.common.configuration.Configuration")}}.class);
                        var projector = application.getBean({{projectorName}}.class);
                        var store = application.getBean({{JavaCodeBuilder.StringLiteral(modelType)}}, {{storeType}}.class);
                        var streams = application.getBean({{JavaNames.Type(query.Name)}}Streams.class);
                        store.clear();
                        try (var client = {{code.Reference("java.net.http.HttpClient")}}.newHttpClient();
                             var executor = {{code.Reference("java.util.concurrent.Executors")}}.newVirtualThreadPerTaskExecutor()) {
                            var request = {{code.Reference("java.net.http.HttpRequest")}}.newBuilder({{code.Reference("java.net.URI")}}.create("http://127.0.0.1:" + port + {{JavaCodeBuilder.StringLiteral(QueryEndpoint.Route(located, query))}}))
                                .header("Accept", "text/event-stream").GET().build();
                            var response = client.sendAsync(request, {{code.Reference("java.net.http.HttpResponse")}}.BodyHandlers.ofInputStream())
                                .get(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS);
                            try (var stream = response.body();
                                 var reader = new {{code.Reference("java.io.BufferedReader")}}(new {{code.Reference("java.io.InputStreamReader")}}(stream, {{code.Reference("java.nio.charset.StandardCharsets")}}.UTF_8))) {
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(200, response.statusCode());
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(response.headers().firstValue("Content-Type").orElse("").startsWith("text/event-stream"));
                                var mapper = application.getBean({{code.Reference("tools.jackson.databind.json.JsonMapper")}}.class);
                                var initial = mapper.readTree(executor.submit(() -> nextData(reader)).get(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS));
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(initial.isArray());
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(0, initial.size());
                                configuration.getComponent({{code.Reference("org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory")}}.class).create()
                                    .executeWithResult(processing -> {
                                        projector.on({{eventArguments(code)}});
                                        return {{code.Reference("java.util.concurrent.CompletableFuture")}}.completedFuture(null);
                                    }).get(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS);
                                var changed = mapper.readTree(executor.submit(() -> nextData(reader)).get(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS));
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(changed.isArray());
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(1, changed.size());
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(mapper.readTree(mapper.writeValueAsString(store.all())), changed);
                                var snapshotRequest = {{code.Reference("java.net.http.HttpRequest")}}.newBuilder(request.uri()).GET().build();
                                var snapshot = client.send(snapshotRequest, {{code.Reference("java.net.http.HttpResponse")}}.BodyHandlers.ofString());
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(200, snapshot.statusCode());
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(snapshot.headers().firstValue("Content-Type").orElse("").startsWith("application/json"));
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(mapper.readTree(mapper.writeValueAsString(store.all())), mapper.readTree(snapshot.body()));
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(1, streams.activeCount());
                                var closing = System.nanoTime();
                                application.close();
                                var elapsed = {{code.Reference("java.time.Duration")}}.ofNanos(System.nanoTime() - closing);
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(elapsed.compareTo({{code.Reference("java.time.Duration")}}.ofSeconds(5)) < 0, "Context close took " + elapsed);
                                {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(0, streams.activeCount());
                            }
                        }
                    } finally {
                        application.close();
                    }
                }

                private static String nextData({{code.Reference("java.io.BufferedReader")}} reader) throws {{code.Reference("java.io.IOException")}} {
                    String line;
                    while ((line = reader.readLine()) != null) {
                        if (line.startsWith("data:")) return line.substring(5).stripLeading();
                    }
                    throw new {{code.Reference("java.io.EOFException")}}("SSE stream ended before data arrived");
                }
            }
            """);
        context.AddJava(package, name, code.Build(), true, query.Id);
    }
}
