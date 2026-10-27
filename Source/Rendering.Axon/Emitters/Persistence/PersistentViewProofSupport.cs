// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewProofSupport
{
    internal const string Source = """
        package __PACKAGE__;

        import __PERSISTENCE__.JpaReadModelStore;
        import __RUNTIME__.ReadModelStore;
        import __RUNTIME__.InMemoryReadModelStore;
        import jakarta.persistence.EntityManagerFactory;
        import java.nio.file.Files;
        import java.nio.file.Path;
        import java.util.ArrayList;
        import java.util.Base64;
        import java.util.Collections;
        import java.util.Collection;
        import java.util.List;
        import java.util.Map;
        import java.util.Set;
        import java.util.TreeMap;
        import java.util.UUID;
        import java.util.concurrent.CompletableFuture;
        import java.util.concurrent.CountDownLatch;
        import java.util.concurrent.TimeUnit;
        import java.util.function.BooleanSupplier;
        import java.util.function.Supplier;
        import java.util.stream.Collectors;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.common.jpa.EntityManagerExecutor;
        import org.axonframework.conversion.GeneralConverter;
        import org.axonframework.messaging.core.LegacyResources;
        import org.axonframework.messaging.core.Message;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.TrackingToken;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.axonframework.eventsourcing.eventstore.EventStore;
        import org.axonframework.eventsourcing.eventstore.SourcingCondition;
        import org.axonframework.messaging.eventstreaming.EventCriteria;
        import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;
        import org.axonframework.messaging.eventhandling.gateway.EventAppender;
        import org.axonframework.messaging.core.unitofwork.ProcessingContext;
        import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
        import org.axonframework.messaging.eventhandling.annotation.EventHandler;
        import org.axonframework.messaging.eventhandling.processing.streaming.segmenting.Segment;
        import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
        import org.springframework.boot.builder.SpringApplicationBuilder;
        import org.springframework.boot.test.context.TestConfiguration;
        import org.springframework.context.ConfigurableApplicationContext;
        import org.springframework.context.annotation.Bean;
        import org.springframework.context.annotation.Import;
        import tools.jackson.databind.json.JsonMapper;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Fixture.tag;

        public final class __NAME__Support {
            private __NAME__Support() {}
            static Path database() throws Exception {
                var path = Path.of("target", "persistent-views", UUID.randomUUID().toString(), "events").toAbsolutePath();
                Files.createDirectories(path.getParent());
                return path;
            }
            static ConfigurableApplicationContext start(Path path, Class<?>... customizations) {
                return new SpringApplicationBuilder(FlatFixture.class).sources(customizations).profiles("persistent").run(
                        "--server.port=0", "--spring.datasource.url=jdbc:h2:file:" + path,
                        "--axon.eventhandling.processors[__PACKAGE__].initial-segment-count=1",
                        "--spring.jpa.hibernate.ddl-auto=update", "--spring.main.banner-mode=off");
            }
            static ConfigurableApplicationContext startMultiSegment(Path path) throws Exception {
                var application = new SpringApplicationBuilder(FlatFixture.class).profiles("persistent").run(
                        "--server.port=0", "--spring.datasource.url=jdbc:h2:file:" + path,
                        "--logging.level.org.axonframework.messaging.eventhandling.processing.streaming=DEBUG",
                        "--axon.eventhandling.processors[__PACKAGE__].initial-segment-count=4",
                        "--spring.jpa.hibernate.ddl-auto=update", "--spring.main.banner-mode=off");
                journalApplication = application;
                try {
                    await(() -> tokens(application).size() == 4);
                    return application;
                } catch (Exception | Error cause) {
                    application.close();
                    throw cause;
                }
            }
            @SuppressWarnings("unchecked")
            static ReadModelStore<Object, __MODEL__> store(ConfigurableApplicationContext application) {
                return (ReadModelStore<Object, __MODEL__>) application.getBean("__QUALIFIER__", ReadModelStore.class);
            }
            static UnitOfWorkFactory units(ConfigurableApplicationContext application) {
                return application.getBean(Configuration.class).getComponent(UnitOfWorkFactory.class);
            }
            static void append(ConfigurableApplicationContext application, Object event) throws Exception {
                var eventStore = application.getBean(Configuration.class).getComponent(EventStore.class);
                units(application).create().executeWithResult(context ->
                        eventStore.transaction(context).source(SourcingCondition.conditionFor(EventCriteria.havingTags(tag(event))))
                                .reduce(0, (count, entry) -> count + 1).thenApply(count -> {
                                    EventAppender.forContext(context).append(event);
                                    return (Void) null;
                                })).get(10, TimeUnit.SECONDS);
            }
            static Set<__MODEL__> normalized(Collection<__MODEL__> rows) {
                return rows.stream().map(__NAME__Support::normalize).collect(Collectors.toSet());
            }
            private static __MODEL__ normalize(__MODEL__ row) {
                __NORMALIZE__
            }
            static __PROJECTOR__ projector(ReadModelStore<Object, __MODEL__> store) {
                return new __PROJECTOR__(store__REDUCER_ARGUMENT__);
            }
            static List<__MODEL__> expected() {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = projector(store);
        __EXPECTED__
                assertFalse(store.all().isEmpty(), "Proof must construct nonempty modeled rows");
                return store.all();
            }
            static List<__MODEL__> next(int replays) {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = projector(store);
                // Reconstruct the same key by replaying the modeled arrangement, not by guessing a row property.
        __EXPECTED__
                for (var replay = 0; replay < replays; replay++) projector.on(__EVENT____ARGUMENT__);
                return store.all();
            }
            // A dedicated non-idempotent view proves atomicity even when the modeled Set is idempotent.
            static ReadModelStore<Object, Integer> counter(ConfigurableApplicationContext application) {
                return new JpaReadModelStore<>(application.getBean(EntityManagerFactory.class), application.getBean(JsonMapper.class),
                        "__QUALIFIER__.atomicity", Integer.class);
            }
            static int count(ConfigurableApplicationContext application) { return counter(application).get("count").orElse(0); }
            static Map<Integer, String> tokens(ConfigurableApplicationContext application) {
                try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                    var tokens = new TreeMap<Integer, String>();
                    for (var row : manager.createQuery("select e.segment, e.token from TokenEntry e where e.processorName = :name", Object[].class)
                            .setParameter("name", "__PACKAGE__").getResultList())
                        tokens.put((Integer) row[0], row[1] == null ? "<null>" : Base64.getEncoder().encodeToString((byte[]) row[1]));
                    return tokens;
                }
            }
            static void await(BooleanSupplier condition) throws Exception {
                await(condition, () -> "");
            }
            static void await(BooleanSupplier condition, Supplier<String> state) throws Exception {
                var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(20);
                while (!condition.getAsBoolean() && System.nanoTime() < deadline) Thread.sleep(25);
                assertTrue(condition.getAsBoolean(), () -> "Persistent processor/query condition timed out" + state.get() + dump(journalApplication));
            }
            // TEMP axon-35: diagnostic journal of every witness delivery, dumped with the AXON35 prefix.
            static final List<String> journal = Collections.synchronizedList(new ArrayList<>());
            static volatile String journalRun = "init";
            static volatile ConfigurableApplicationContext journalApplication;
            static String dump(ConfigurableApplicationContext application) {
                var out = new StringBuilder();
                synchronized (journal) {
                    for (var line : journal) out.append("\nAXON35 journal ").append(line);
                }
                if (application == null || !application.isActive()) return out.append("\nAXON35 application inactive").toString();
                try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                    for (var row : manager.createQuery("select e.globalIndex, e.identifier, e.aggregateIdentifier from AggregateEventEntry e order by e.globalIndex", Object[].class).getResultList())
                        out.append("\nAXON35 event index=").append(row[0]).append(" id=").append(row[1]).append(" aggregate=").append(row[2]);
                    var converter = application.getBean(GeneralConverter.class);
                    for (var entry : manager.createQuery("select e from TokenEntry e where e.processorName = :name", TokenEntry.class)
                            .setParameter("name", "__PACKAGE__").getResultList())
                        out.append("\nAXON35 token segment=").append(entry.getSegment().getSegmentId()).append(" ").append(entry.getToken(converter));
                } catch (RuntimeException cause) {
                    out.append("\nAXON35 dump failed: ").append(cause);
                }
                return out.toString();
            }
            static void record(ProcessingContext context, Segment segment, Integer owner) {
                var aggregate = context.getResource(LegacyResources.AGGREGATE_IDENTIFIER_KEY);
                var line = journalRun + " thread=" + Thread.currentThread().getName() + " segment=" + owner + " mask=" + segment.getMask()
                        + " token=" + TrackingToken.fromContext(context).orElse(null) + " message=" + Message.fromContext(context).identifier()
                        + " aggregate=" + aggregate + " hash&mask=" + (aggregate == null ? "n/a" : (aggregate.hashCode() & segment.getMask()));
                journal.add(line + " outcome=delivered");
                context.runOnAfterCommit(ignored -> journal.add(line + " outcome=committed"));
                context.onError((ignored, phase, cause) -> journal.add(line + " outcome=error " + phase + " " + cause));
            }
            enum Failure { NONE, HANDLER, TOKEN, ASYNC, NON_JOINED }
            static volatile Failure failure = Failure.NONE;
            static volatile Integer observedOwner;
            static final Map<org.axonframework.messaging.eventstreaming.Tag, Integer> observedOwners = new java.util.concurrent.ConcurrentHashMap<>();
            static volatile Integer tokenFailureSegment;
            static volatile CountDownLatch rolledBack = new CountDownLatch(1);
            static volatile CountDownLatch refused = new CountDownLatch(1);
            static volatile CountDownLatch viewChanged = new CountDownLatch(1);
            static volatile CountDownLatch tokenRejected = new CountDownLatch(1);
            @org.axonframework.messaging.core.annotation.Namespace("__PACKAGE__")
            public static class RollbackWitness {
                private final ConfigurableApplicationContext application;
                RollbackWitness(ConfigurableApplicationContext application) { this.application = application; }
                @EventHandler
                public void handle(__EVENT_TYPE__ event, ProcessingContext context) {
                    var owner = Segment.fromContext(context).map(Segment::getSegmentId)
                            .orElseThrow(() -> new AssertionError("FIXTURE: pooled witness requires an owning segment"));
                    observedOwner = owner;
                    observedOwners.put(tag(event), owner);
                    record(context, Segment.fromContext(context).orElseThrow(), owner);
                    context.onError((ignored, phase, cause) -> rolledBack.countDown());
                    var bound = JpaReadModelStore.bind(store(application), context);
                    if (failure == Failure.ASYNC) {
                        var cause = assertThrows(java.util.concurrent.CompletionException.class,
                                () -> CompletableFuture.runAsync(bound::clear).join()).getCause();
                        assertInstanceOf(IllegalStateException.class, cause);
                        assertTrue(cause.getMessage().contains("handler thread"));
                        refused.countDown();
                        throw new IllegalStateException("off-thread write refused", cause);
                    }
                    if (failure == Failure.NON_JOINED) {
                        try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                            assertFalse(manager.isJoinedToTransaction());
                            var unjoined = context.withResource(JpaTransactionalExecutorProvider.SUPPLIER_KEY,
                                    () -> new EntityManagerExecutor(() -> manager));
                            var cause = assertThrows(java.util.concurrent.CompletionException.class,
                                    () -> JpaReadModelStore.bind(store(application), unjoined).clear()).getCause();
                            assertInstanceOf(IllegalStateException.class, cause);
                            assertTrue(cause.getMessage().contains("processor's active transaction"));
                            refused.countDown();
                            throw new IllegalStateException("non-joined write refused", cause);
                        }
                    }
                    var changing = JpaReadModelStore.bind(counter(application), context);
                    var before = changing.get("count").orElse(0);
                    changing.update("count", current -> (current == null ? 0 : current) + 1);
                    assertEquals(before + 1, changing.get("count").orElseThrow(), "Actual handler must flush a changed view row");
                    if (failure != Failure.NONE) viewChanged.countDown();
                    if (failure == Failure.HANDLER) {
                        projector(bound).on(event__ARGUMENT__);
                        throw new IllegalStateException("deliberate handler failure after view write");
                    }
                }
            }
            /** Real database rejection inside JpaTokenStore.storeToken, not a prepare-commit callback. */
            public static class TokenFailure implements org.h2.api.Trigger {
                private int tokenColumn;
                private int processorColumn;
                private int segmentColumn;
                @Override public void init(java.sql.Connection connection, String schema, String trigger, String table,
                        boolean before, int type) throws java.sql.SQLException {
                    try (var statement = connection.createStatement(); var result = statement.executeQuery("select * from token_entry where 1=0")) {
                        for (var index = 1; index <= result.getMetaData().getColumnCount(); index++) {
                            var name = result.getMetaData().getColumnName(index);
                            if (name.equalsIgnoreCase("token")) tokenColumn = index - 1;
                            if (name.equalsIgnoreCase("processor_name")) processorColumn = index - 1;
                            if (name.equalsIgnoreCase("segment")) segmentColumn = index - 1;
                        }
                    }
                }
                @Override public void fire(java.sql.Connection connection, Object[] before, Object[] after) throws java.sql.SQLException {
                    if (failure == Failure.TOKEN && "__PACKAGE__".equals(after[processorColumn]) &&
                            (tokenFailureSegment == null || tokenFailureSegment.equals(after[segmentColumn])) &&
                            !java.util.Arrays.equals(bytes(before[tokenColumn]), bytes(after[tokenColumn]))) {
                        tokenRejected.countDown();
                        throw new java.sql.SQLException("deliberate TokenStore persistence failure", "23514");
                    }
                }
                private byte[] bytes(Object value) throws java.sql.SQLException {
                    return value instanceof java.sql.Blob blob ? blob.getBytes(1, Math.toIntExact(blob.length())) : (byte[]) value;
                }
            }
            static void installTokenFailure(Path path) throws Exception { installTokenFailure(path, null); }
            static void installTokenFailure(Path path, Integer owner) throws Exception {
                tokenFailureSegment = owner;
                try (var connection = java.sql.DriverManager.getConnection("jdbc:h2:file:" + path, "sa", "");
                     var statement = connection.createStatement()) {
                    statement.execute("create trigger stage_token_failure before update on token_entry for each row call '__PACKAGE__.__NAME__Support$TokenFailure'");
                }
            }
            /** Scan-free imports of the real emitted flat projector, query and persistence infrastructure. */
            @TestConfiguration(proxyBeanMethods = false)
            @EnableAutoConfiguration
            @Import({ __IMPORTS__ })
            static class FlatFixture {
                @Bean RollbackWitness rollbackWitness(ConfigurableApplicationContext application) { return new RollbackWitness(application); }
            }
        }
        """;
}
