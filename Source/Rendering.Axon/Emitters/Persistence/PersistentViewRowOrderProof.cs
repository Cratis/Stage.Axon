// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewRowOrderProof
{
    internal const string Source = """
        package __PACKAGE__;

        import __RUNTIME_MODULE__.InMemoryReadModelStore;
        import jakarta.persistence.EntityManagerFactory;
        import java.util.Set;
        import java.util.concurrent.TimeUnit;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.conversion.GeneralConverter;
        import org.axonframework.eventsourcing.eventstore.EventStore;
        import org.axonframework.eventsourcing.eventstore.AggregateBasedEventStorageEngineUtils;
        import org.axonframework.messaging.eventhandling.processing.streaming.StreamingEventProcessor;
        import org.axonframework.messaging.eventhandling.processing.streaming.segmenting.Segment;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.TrackingToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.WrappedToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.junit.jupiter.api.Test;
        import org.springframework.context.ConfigurableApplicationContext;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;
        import static __PACKAGE__.__NAME__Fixture.tag;

        /** Hold only the old source segment; row routing must not depend on that source. */
        class __NAME__RowOrderTest {
            @Test void differentSourcesWriteOneRowInStreamOrder() throws Exception {
                var segments = Segment.splitBalanced(Segment.ROOT_SEGMENT, 3);
                var rowSegment = segments.stream().filter(segment -> segment.matches(__ROW_KEY__)).findFirst().orElseThrow();
                var held = segments.stream().filter(segment -> !segment.equals(rowSegment)).findFirst().orElseThrow();
                var firstSource = sourceFor(held);
                var competingSource = sourceFor(rowSegment);
                var first = __ROW_FIRST__;
                var competing = __ROW_COMPETING__;
                __ROW_LATER__
                assertTrue(held.matches(AggregateBasedEventStorageEngineUtils.resolveAggregateIdentifier(Set.of(tag(first)))));
                assertTrue(rowSegment.matches(AggregateBasedEventStorageEngineUtils.resolveAggregateIdentifier(Set.of(tag(competing)))));
                assertNotEquals(held, rowSegment, "FIXTURE: default routing must separate the competing deliveries");
                var reference = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = projector(reference);
                projector.on(first__ARGUMENT__);
                projector.on(first__ARGUMENT__);
                projector.on(competing__ARGUMENT__);
                __ROW_REFERENCE_LATER__
                var inverted = new InMemoryReadModelStore<Object, __MODEL__>();
                var reversed = projector(inverted);
                reversed.on(first__ARGUMENT__);
                reversed.on(competing__ARGUMENT__);
                __ROW_INVERTED_LATER__
                reversed.on(first__ARGUMENT__);
                assertNotEquals(normalized(reference.all()), normalized(inverted.all()),
                        "FIXTURE: the last writer must expose inversion, with no later masking event");
                try (var application = startMultiSegment(database())) {
                    var processor = application.getBean(Configuration.class).getComponents(StreamingEventProcessor.class).get("__PACKAGE__");
                    assertNotNull(processor);
                    await(() -> processor.processingStatus().size() == 4,
                            () -> ": FIXTURE must claim four segments; active=" + processor.processingStatus().keySet());
                    append(application, first);
                    await(() -> observedOwners.containsKey(tag(first)) && !store(application).all().isEmpty());
                    var seedOwner = observedOwners.get(tag(first));
                    assertTrue(seedOwner == held.getSegmentId() || seedOwner == rowSegment.getSegmentId(),
                            "FIXTURE: seed owner must be the default-source or row-key segment: " + seedOwner);
                    var initialHead = head(application);
                    // Idle workers may never persist skipped-event progress. Only a delivery's owner must commit it.
                    await(() -> coversHead(application, initialHead, seedOwner),
                            () -> ": initial seed owner=" + seedOwner + ", head=" + initialHead + ", tokens=" + tokens(application));
                    // A long release cannot expire before the competing transaction commits.
                    processor.releaseSegment(held.getSegmentId(), 1, TimeUnit.DAYS).get(10, TimeUnit.SECONDS);
                    await(() -> !processor.processingStatus().containsKey(held.getSegmentId()));
                    append(application, first);
                    var heldHead = head(application);
                    append(application, competing);
                    __ROW_APPEND_LATER__
                    var terminalHead = head(application);
                    assertFalse(initialHead.covers(heldHead), "FIXTURE: held delivery must advance the stream");
                    assertFalse(heldHead.covers(terminalHead), "FIXTURE: competing delivery must advance the stream");
                    // The competing owner's durable token proves its transaction committed before reclaim.
                    await(() -> coversHead(application, terminalHead, rowSegment.getSegmentId()),
                            () -> ": competing owner=" + rowSegment.getSegmentId() + ", head=" + terminalHead + ", tokens=" + tokens(application));
                    assertFalse(processor.processingStatus().containsKey(held.getSegmentId()), "Held segment must not resume before competing commit");
                    assertTrue(processor.claimSegment(held.getSegmentId()).get(10, TimeUnit.SECONDS));
                    await(() -> processor.processingStatus().containsKey(held.getSegmentId()));
                    // Before the fix this is the reclaimed source owner; after the fix it is the row owner.
                    // Its last relevant event is heldHead, not the later event skipped by that idle worker.
                    await(() -> coversHead(application, heldHead, seedOwner),
                            () -> ": held delivery owner=" + seedOwner + ", head=" + heldHead + ", tokens=" + tokens(application));
                    assertEquals(normalized(reference.all()), normalized(store(application).all()),
                            "Events for one persistent row must apply in stream order across event sources");
                }
            }
            private static String sourceFor(Segment segment) {
                for (var index = 0; index < 1000; index++) {
                    var source = "row-source-" + index;
                    if (segment.matches(source)) return source;
                }
                throw new AssertionError("FIXTURE: no source for " + segment);
            }
            private static TrackingToken head(ConfigurableApplicationContext application) throws Exception {
                var events = application.getBean(Configuration.class).getComponent(EventStore.class);
                return units(application).create().executeWithResult(events::latestToken).get(10, TimeUnit.SECONDS);
            }
            private static boolean coversHead(ConfigurableApplicationContext application, TrackingToken head, int owner) {
                assertNotNull(head, "FIXTURE: appended stream must have a head");
                var converter = application.getBean(Configuration.class).getComponent(GeneralConverter.class);
                try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                    var entry = manager.createQuery("select e from TokenEntry e where e.processorName = :name", TokenEntry.class)
                            .setParameter("name", "__PACKAGE__").getResultList().stream()
                            .filter(candidate -> candidate.getSegment().getSegmentId() == owner).findFirst().orElse(null);
                    if (entry == null) return false;
                    return covers(entry.getToken(converter), head);
                }
            }
            private static boolean covers(TrackingToken token, TrackingToken head) {
                if (token == null) return false;
                var lower = WrappedToken.unwrapLowerBound(token);
                var upper = WrappedToken.unwrapUpperBound(token);
                return reachedPosition(lower, head) && reachedPosition(upper, head);
            }
            private static boolean reachedPosition(TrackingToken token, TrackingToken head) {
                if (token == null) return false;
                var position = token.position();
                var headPosition = head.position();
                // This owner-only committed-delivery barrier must not wait for cleanup of historical gaps.
                return position.isPresent() && headPosition.isPresent() && position.getAsLong() >= headPosition.getAsLong();
            }
        }
        """;
}
