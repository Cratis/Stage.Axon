// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewProof
{
    internal const string Source = """
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
        import static __PACKAGE__.__NAME__Fixture.assertFixture;

        /** Default Surefire test: real emitted projector, pooled processor, durable token and two boots. */
        class __NAME__Test {
            @Test
            void recoveredRowsAreQueryableBeforeAnyNewAppend() throws Exception {
                var path = database();
                var initial = expected();
                java.util.Map<Integer, String> checkpoint;
                try (var application = start(path)) {
                    var before = tokens(application);
        __APPENDS__
                    assertFixture(application, before);
                    await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(initial)) && count(application) == __COUNT__ && tokens(application).values().stream().anyMatch(value -> !value.equals("<null>")));
                    checkpoint = tokens(application);
                    assertFalse(checkpoint.isEmpty());
                    assertEquals(1, checkpoint.size(), "Single-segment proof requires exactly one processor segment");
                }
                try (var application = start(path)) {
                    assertEquals(new HashSet<>(initial), new HashSet<>(store(application).all()));
                    assertEquals(checkpoint, tokens(application), "Retain the real checkpoint; no reset/replay adoption");
                    assertQuery(application, initial);
                    assertEquals(__COUNT__, count(application), "Non-idempotent proof rows must survive restart");
                    var subsequent = next(1);
                    append(application, __EVENT__);
                    await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(subsequent)) && count(application) == __COUNT__ + 1 && !tokens(application).equals(checkpoint));
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
                var afterRetry = next(1);
                failure = Failure.NONE;
                try {
                    try (var application = start(path)) {
                        assertInstanceOf(JpaReadModelStore.class, store(application));
                        var before = tokens(application);
        __APPENDS__
                        assertFixture(application, before);
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(initial)) && count(application) == __COUNT__ && tokens(application).values().stream().anyMatch(value -> !value.equals("<null>")));
                        var checkpoint = tokens(application);
                        assertFalse(checkpoint.isEmpty(), "Real processor checkpoint rows must exist");
                        assertEquals(1, checkpoint.size(), "Single-segment proof requires exactly one processor segment");
                        assertQuery(application, initial);
                        rolledBack = new java.util.concurrent.CountDownLatch(1);
                        refused = new java.util.concurrent.CountDownLatch(1);
                        viewChanged = new java.util.concurrent.CountDownLatch(1);
                        tokenRejected = new java.util.concurrent.CountDownLatch(1);
                        if (requested == Failure.TOKEN) installTokenFailure(path);
                        failure = requested;
                        append(application, __EVENT__);
                        assertTrue(rolledBack.await(20, TimeUnit.SECONDS), "Actual processor must roll back: " + requested);
                        if (requested == Failure.ASYNC || requested == Failure.NON_JOINED)
                            assertTrue(refused.await(20, TimeUnit.SECONDS), "Actual handler must reach the transaction/thread guard");
                        else
                            assertTrue(viewChanged.await(20, TimeUnit.SECONDS), "Rollback must undo a demonstrably changed counter row");
                        if (requested == Failure.TOKEN)
                            assertTrue(tokenRejected.await(20, TimeUnit.SECONDS), "Real JpaTokenStore update must reach the rejecting trigger");
                        assertEquals(__COUNT__, count(application), "Non-idempotent view change must roll back");
                        assertEquals(new HashSet<>(initial), new HashSet<>(store(application).all()), "No independently committed projection write");
                        // Single-segment proof: native run 40395678690 showed public sequenceIdentifierFor + Segment.matches selected segment 2 while pooled routing used segment 13. See the sibling __NAME__MultiSegmentTest for observed-owner multi-segment rollback coverage.
                        assertEquals(checkpoint, tokens(application), "Failed processing must not advance the actual checkpoint");
                    }
                    failure = Failure.NONE;
                    try (var application = start(path)) {
                        // The failed event must resume from the retained checkpoint exactly once.
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(afterRetry)) && count(application) == __COUNT__ + 1);
                        var checkpoint = tokens(application);
                        assertFalse(checkpoint.isEmpty());
                        assertQuery(application, afterRetry);
                        var subsequent = next(2);
                        append(application, __EVENT__);
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(subsequent)) && count(application) == __COUNT__ + 2 && !tokens(application).equals(checkpoint));
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

    internal const string MultiSegmentSource = """
        package __PACKAGE__;

        import jakarta.persistence.EntityManagerFactory;
        import java.net.URI;
        import java.net.http.HttpClient;
        import java.net.http.HttpRequest;
        import java.net.http.HttpResponse;
        import java.util.HashSet;
        import java.util.Map;
        import java.util.TreeMap;
        import java.util.concurrent.TimeUnit;
        import org.axonframework.conversion.GeneralConverter;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.TrackingToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.WrappedToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.junit.jupiter.api.Test;
        import org.springframework.context.ConfigurableApplicationContext;
        import org.springframework.boot.web.server.context.WebServerApplicationContext;
        import tools.jackson.databind.json.JsonMapper;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;
        import static __PACKAGE__.__NAME__Fixture.assertFixture;
        import static __PACKAGE__.__NAME__Fixture.tag;

        /** Four-segment sibling: observe ownership in pooled delivery, never recompute routing. */
        class __NAME__MultiSegmentTest {
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
                var afterRetry = next(1);
                failure = Failure.NONE;
                observedOwner = null;
                observedOwners.clear();
                try {
                    try (var application = startMultiSegment(path)) {
                        assertInstanceOf(__PERSISTENCE__.JpaReadModelStore.class, store(application));
                        var before = tokens(application);
        __APPENDS__
                        assertFixture(application, before);
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(initial)) && count(application) == __COUNT__ && tokens(application).values().stream().anyMatch(value -> !value.equals("<null>")));
                        var checkpoint = tokens(application);
                        var owner = observedOwners.get(tag(__EVENT__));
                        assertNotNull(owner, "FIXTURE: arrangement must observe the failed event's owning segment");
                        assertEquals(4, checkpoint.size(), "FIXTURE: multi-segment proof requires four processor segments");
                        assertTrue(checkpoint.containsKey(owner), "FIXTURE: observed owner must have a checkpoint");
                        assertNotEquals("<null>", checkpoint.get(owner), "FIXTURE: observed owner must have a persisted token");
                        var decodedCheckpoint = decodedTokens(application);
                        assertEquals(checkpoint.keySet(), decodedCheckpoint.keySet(), "FIXTURE: decode every checkpoint segment");
                        assertQuery(application, initial);
                        rolledBack = new java.util.concurrent.CountDownLatch(1);
                        refused = new java.util.concurrent.CountDownLatch(1);
                        viewChanged = new java.util.concurrent.CountDownLatch(1);
                        tokenRejected = new java.util.concurrent.CountDownLatch(1);
                        if (requested == Failure.TOKEN) installTokenFailure(path, owner);
                        observedOwner = null;
                        failure = requested;
                        append(application, __EVENT__);
                        assertTrue(rolledBack.await(20, TimeUnit.SECONDS), "Actual processor must roll back: " + requested);
                        assertEquals(owner, observedOwner, "FIXTURE: failed delivery must use the observed owner");
                        if (requested == Failure.ASYNC || requested == Failure.NON_JOINED)
                            assertTrue(refused.await(20, TimeUnit.SECONDS), "Actual handler must reach the transaction/thread guard");
                        else
                            assertTrue(viewChanged.await(20, TimeUnit.SECONDS), "Rollback must undo a demonstrably changed counter row");
                        if (requested == Failure.TOKEN)
                            assertTrue(tokenRejected.await(20, TimeUnit.SECONDS), "Real JpaTokenStore update must reach the owner-scoped rejecting trigger");
                        assertEquals(__COUNT__, count(application), "Non-idempotent view change must roll back");
                        assertEquals(new HashSet<>(initial), new HashSet<>(store(application).all()), "No independently committed projection write");
                        assertRollbackTokens(application, owner, checkpoint, decodedCheckpoint);
                    }
                    failure = Failure.NONE;
                    try (var application = startMultiSegment(path)) {
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(afterRetry)) && count(application) == __COUNT__ + 1);
                        assertQuery(application, afterRetry);
                        var checkpoint = tokens(application);
                        assertEquals(4, checkpoint.size(), "Reclaim must retain all four segments");
                        var subsequent = next(2);
                        append(application, __EVENT__);
                        await(() -> new HashSet<>(store(application).all()).equals(new HashSet<>(subsequent)) && count(application) == __COUNT__ + 2 && !tokens(application).equals(checkpoint));
                        assertQuery(application, subsequent);
                    }
                } finally {
                    failure = Failure.NONE;
                    tokenFailureSegment = null;
                }
            }
            private Map<Integer, TrackingToken> decodedTokens(ConfigurableApplicationContext application) {
                var converter = application.getBean(GeneralConverter.class);
                try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                    var checkpoint = new TreeMap<Integer, TrackingToken>();
                    for (var entry : manager.createQuery("select e from TokenEntry e where e.processorName = :name", TokenEntry.class)
                            .setParameter("name", "__PACKAGE__").getResultList())
                        checkpoint.put(entry.getSegment().getSegmentId(), entry.getToken(converter));
                    return checkpoint;
                }
            }
            private void assertRollbackTokens(ConfigurableApplicationContext application, Integer owner,
                    Map<Integer, String> checkpoint, Map<Integer, TrackingToken> decodedCheckpoint) {
                var after = tokens(application);
                assertEquals(checkpoint.keySet(), after.keySet(), "Failure must not change the segment key set");
                assertEquals(checkpoint.get(owner), after.get(owner), "Failed owner checkpoint must remain byte-identical");
                var decodedAfter = decodedTokens(application);
                assertEquals(checkpoint.keySet(), decodedAfter.keySet(), "Failure must retain every decoded segment");
                for (var segment : checkpoint.keySet()) {
                    if (segment.equals(owner)) continue;
                    assertCovers(WrappedToken.unwrapLowerBound(decodedAfter.get(segment)),
                            WrappedToken.unwrapLowerBound(decodedCheckpoint.get(segment)), segment);
                    assertCovers(WrappedToken.unwrapUpperBound(decodedAfter.get(segment)),
                            WrappedToken.unwrapUpperBound(decodedCheckpoint.get(segment)), segment);
                }
            }
            private void assertCovers(TrackingToken after, TrackingToken before, int segment) {
                // A null bound is the initial position; a non-null bound must never regress to it.
                if (before == null) return;
                assertNotNull(after, "Non-owner token must not regress to null: " + segment);
                assertTrue(after.covers(before), "Non-owner token must cover its prior position: " + segment);
            }
            private void assertQuery(ConfigurableApplicationContext application, java.util.List<__MODEL__> rows) throws Exception {
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
