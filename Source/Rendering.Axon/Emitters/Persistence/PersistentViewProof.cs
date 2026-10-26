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
        import jakarta.persistence.EntityManagerFactory;
        import java.net.URI;
        import java.net.http.HttpClient;
        import java.net.http.HttpRequest;
        import java.net.http.HttpResponse;
        import java.util.HashSet;
        import java.util.Map;
        import java.util.TreeMap;
        import java.util.concurrent.TimeUnit;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.conversion.GeneralConverter;
        import org.axonframework.eventsourcing.eventstore.EventStore;
        import org.axonframework.eventsourcing.eventstore.SourcingCondition;
        import org.axonframework.messaging.core.sequencing.SequencingPolicy;
        import org.axonframework.messaging.eventhandling.EventHandlingComponent;
        import org.axonframework.messaging.eventhandling.EventMessage;
        import org.axonframework.messaging.eventhandling.conversion.EventConverter;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.TrackingToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.WrappedToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.axonframework.messaging.eventstreaming.EventCriteria;
        import org.junit.jupiter.api.Test;
        import org.springframework.boot.web.server.context.WebServerApplicationContext;
        import org.springframework.context.ConfigurableApplicationContext;
        import tools.jackson.databind.json.JsonMapper;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;
        import static __PACKAGE__.__NAME__Fixture.assertFixture;
        import static __PACKAGE__.__NAME__Fixture.tag;

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
                        var segmentCheckpoint = tokenEntries(application);
                        assertQuery(application, initial);
                        rolledBack = new java.util.concurrent.CountDownLatch(1);
                        refused = new java.util.concurrent.CountDownLatch(1);
                        viewChanged = new java.util.concurrent.CountDownLatch(1);
                        tokenRejected = new java.util.concurrent.CountDownLatch(1);
                        if (requested == Failure.TOKEN) installTokenFailure(path);
                        failure = requested;
                        var failedEvent = __EVENT__;
                        append(application, failedEvent);
                        assertTrue(rolledBack.await(20, TimeUnit.SECONDS), "Actual processor must roll back: " + requested);
                        if (requested == Failure.ASYNC || requested == Failure.NON_JOINED)
                            assertTrue(refused.await(20, TimeUnit.SECONDS), "Actual handler must reach the transaction/thread guard");
                        else
                            assertTrue(viewChanged.await(20, TimeUnit.SECONDS), "Rollback must undo a demonstrably changed counter row");
                        if (requested == Failure.TOKEN)
                            assertTrue(tokenRejected.await(20, TimeUnit.SECONDS), "Real JpaTokenStore update must reach the rejecting trigger");
                        assertEquals(__COUNT__, count(application), "Non-idempotent view change must roll back");
                        assertEquals(new HashSet<>(initial), new HashSet<>(store(application).all()), "No independently committed projection write");
                        assertRollbackTokens(application, checkpoint, segmentCheckpoint, failedEvent);
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
            private Map<Integer, TokenEntry> tokenEntries(ConfigurableApplicationContext application) {
                try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                    var entries = new TreeMap<Integer, TokenEntry>();
                    for (var entry : manager.createQuery("select e from TokenEntry e where e.processorName = :name", TokenEntry.class)
                            .setParameter("name", "__PACKAGE__").getResultList())
                        entries.put(entry.getSegment().getSegmentId(), entry);
                    return entries;
                }
            }
            private int owningSegment(ConfigurableApplicationContext application, Object event, Map<Integer, TokenEntry> checkpoint) throws Exception {
                var configuration = application.getBean(Configuration.class);
                var eventStore = configuration.getComponent(EventStore.class);
                return units(application).create().executeWithResult(context ->
                        eventStore.transaction(context).source(SourcingCondition.conditionFor(EventCriteria.havingTags(tag(event))))
                                .reduce((EventMessage) null, (last, entry) -> entry.message()).thenApply(last -> {
                                    assertNotNull(last, "Failed event must exist in the actual event store");
                                    var message = last.withConvertedPayload(event.getClass(), configuration.getComponent(EventConverter.class));
                                    assertEquals(event, message.payload(), "Resolve ownership from the actual failed event");
                                    var identifiers = new HashSet<Object>();
                                    configuration.getComponents(EventHandlingComponent.class).values().stream()
                                            .filter(component -> component.supports(message.type().qualifiedName()))
                                            .forEach(component -> identifiers.add(component.sequenceIdentifierFor(message, context)));
                                    assertFalse(identifiers.isEmpty(), "Failed event must have configured handling components");
                                    assertFalse(identifiers.contains(SequencingPolicy.BROADCAST), "Failed event must have a single owning segment, not broadcast");
                                    var owners = checkpoint.values().stream().map(TokenEntry::getSegment)
                                            .filter(segment -> identifiers.stream().anyMatch(segment::matches))
                                            .map(segment -> segment.getSegmentId()).toList();
                                    assertEquals(1, owners.size(), "Failed event sequencing identifiers must resolve exactly one owner: " + owners);
                                    return owners.getFirst();
                                })).get(10, TimeUnit.SECONDS);
            }
            private void assertRollbackTokens(ConfigurableApplicationContext application, Map<Integer, String> checkpoint,
                    Map<Integer, TokenEntry> segmentCheckpoint, Object failedEvent) throws Exception {
                var owner = owningSegment(application, failedEvent, segmentCheckpoint);
                var actual = tokens(application);
                var entries = tokenEntries(application);
                assertEquals(checkpoint.keySet(), segmentCheckpoint.keySet(), "Checkpoint snapshots must contain the same segments");
                assertEquals(checkpoint.keySet(), actual.keySet(), "Rollback must retain every segment checkpoint");
                assertEquals(checkpoint.keySet(), entries.keySet(), "Decoded rollback tokens must retain every segment");
                assertEquals(checkpoint.get(owner), actual.get(owner), "Owning segment " + owner + " must retain its byte-identical checkpoint after failed handling");
                var converter = application.getBean(GeneralConverter.class);
                // Pooled non-owners consume skipped events without invoking handlers; compare progress, not bytes.
                for (var segment : checkpoint.keySet()) {
                    if (segment == owner) continue;
                    var before = segmentCheckpoint.get(segment).getToken(converter);
                    var after = entries.get(segment).getToken(converter);
                    assertTrue(covers(WrappedToken.unwrapLowerBound(after), WrappedToken.unwrapLowerBound(before))
                                    && covers(WrappedToken.unwrapUpperBound(after), WrappedToken.unwrapUpperBound(before)),
                            "Non-owning segment " + segment + " may stay or advance past skipped events, never go backwards");
                }
            }
            private boolean covers(TrackingToken after, TrackingToken before) {
                return before == null || (after != null && after.covers(before));
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
