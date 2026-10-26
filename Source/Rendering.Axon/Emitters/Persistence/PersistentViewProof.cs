// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewProof
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import __PERSISTENCE__.JpaReadModelStore;
        import java.net.URI;
        import java.net.http.HttpClient;
        import java.net.http.HttpRequest;
        import java.net.http.HttpResponse;
        import java.util.HashSet;
        import java.util.concurrent.TimeUnit;
        import org.junit.jupiter.api.Test;
        import org.springframework.boot.web.server.context.WebServerApplicationContext;
        import tools.jackson.databind.json.JsonMapper;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;

        /** Default Surefire test: real emitted projector, pooled processor, durable token and two boots. */
        class __NAME__Test {
            @Test
            void recoveredRowsAreQueryableBeforeAnyNewAppend() throws Exception {
                var path = database();
                var initial = expected();
                java.util.Map<Integer, String> checkpoint;
                try (var application = start(path)) {
        __APPENDS__
                    await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(initial)) && tokens(application).values().stream().anyMatch(value -> !value.equals("<null>")));
                    checkpoint = tokens(application);
                    assertFalse(checkpoint.isEmpty());
                }
                try (var application = start(path)) {
                    assertEquals(new HashSet<>(initial), new HashSet<>(store(application).all()));
                    assertEquals(checkpoint, tokens(application), "Retain the real checkpoint; no reset/replay adoption");
                    assertQuery(application, initial);
                    var subsequent = next(initial);
                    assertNotEquals(new HashSet<>(initial), new HashSet<>(subsequent));
                    append(application, __EVENT__);
                    await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(subsequent)) && !tokens(application).equals(checkpoint));
                    assertQuery(application, subsequent);
                }
            }
            @Test
            void handlerThrowsAfterViewWriteAndReclaimAppliesExactlyOnce() throws Exception {
                assertRollbackAndReclaim(Failure.HANDLER);
            }
            @Test
            void realTokenStorePersistenceFailureRollsBackViewWrite() throws Exception {
                assertRollbackAndReclaim(Failure.TOKEN);
            }
            @Test
            void actualHandlerRefusesOffThreadWriteWithoutChangingRowsOrToken() throws Exception {
                assertRollbackAndReclaim(Failure.ASYNC);
            }
            @Test
            void actualHandlerRefusesNonJoinedTransactionWithoutChangingRowsOrToken() throws Exception {
                assertRollbackAndReclaim(Failure.NON_JOINED);
            }
            private void assertRollbackAndReclaim(Failure requested) throws Exception {
                var path = database();
                var initial = expected();
                var afterRetry = next(initial);
                assertNotEquals(new HashSet<>(initial), new HashSet<>(afterRetry), "Proof event must change modeled state");
                failure = Failure.NONE;
                try {
                    try (var application = start(path)) {
                        assertInstanceOf(JpaReadModelStore.class, store(application));
        __APPENDS__
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(initial)) && tokens(application).values().stream().anyMatch(value -> !value.equals("<null>")));
                        var checkpoint = tokens(application);
                        assertFalse(checkpoint.isEmpty(), "Real processor checkpoint rows must exist");
                        assertQuery(application, initial);
                        rolledBack = new java.util.concurrent.CountDownLatch(1);
                        refused = new java.util.concurrent.CountDownLatch(1);
                        if (requested == Failure.TOKEN) installTokenFailure(path);
                        failure = requested;
                        append(application, __EVENT__);
                        assertTrue(rolledBack.await(20, TimeUnit.SECONDS), "Actual processor must roll back: " + requested);
                        if (requested == Failure.ASYNC || requested == Failure.NON_JOINED)
                            assertTrue(refused.await(20, TimeUnit.SECONDS), "Actual handler must reach the transaction/thread guard");
                        assertEquals(new HashSet<>(initial), new HashSet<>(store(application).all()), "No independently committed projection write");
                        assertEquals(checkpoint, tokens(application), "Failed processing must not advance the actual checkpoint");
                    }
                    failure = Failure.NONE;
                    try (var application = start(path)) {
                        // The failed event must resume from the retained checkpoint exactly once.
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(afterRetry)));
                        var checkpoint = tokens(application);
                        assertFalse(checkpoint.isEmpty());
                        assertQuery(application, afterRetry);
                        var subsequent = next(afterRetry);
                        assertNotEquals(new HashSet<>(afterRetry), new HashSet<>(subsequent));
                        append(application, __EVENT__);
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(subsequent)) && !tokens(application).equals(checkpoint));
                        assertQuery(application, subsequent);
                    }
                } finally { failure = Failure.NONE; }
            }
            private void assertQuery(org.springframework.context.ConfigurableApplicationContext application, java.util.List<__MODEL__> rows) throws Exception {
                var port = ((WebServerApplicationContext) application).getWebServer().getPort();
                var request = HttpRequest.newBuilder(URI.create("http://localhost:" + port + "__ROUTE__")).GET().build();
                try (var client = HttpClient.newHttpClient()) {
                    var response = client.send(request, HttpResponse.BodyHandlers.ofString());
                    assertEquals(200, response.statusCode());
                    var mapper = application.getBean(JsonMapper.class);
                    var actual = mapper.readValue(response.body(), new tools.jackson.core.type.TypeReference<java.util.List<__MODEL__>>() {});
                    assertEquals(new HashSet<>(rows), new HashSet<>(actual), "HTTP query must read recovered durable rows");
                }
            }
        }
        """;
}
