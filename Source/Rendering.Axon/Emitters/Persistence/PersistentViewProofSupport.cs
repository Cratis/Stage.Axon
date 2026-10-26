// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewProofSupport
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import __PERSISTENCE__.JpaReadModelStore;
        import __RUNTIME__.ReadModelStore;
        import __RUNTIME__.InMemoryReadModelStore;
        import jakarta.persistence.EntityManagerFactory;
        import java.nio.file.Files;
        import java.nio.file.Path;
        import java.util.Base64;
        import java.util.List;
        import java.util.Map;
        import java.util.TreeMap;
        import java.util.UUID;
        import java.util.concurrent.CompletableFuture;
        import java.util.concurrent.CountDownLatch;
        import java.util.concurrent.TimeUnit;
        import java.util.function.BooleanSupplier;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.common.jpa.EntityManagerExecutor;
        import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;
        import org.axonframework.messaging.eventhandling.gateway.EventAppender;
        import org.axonframework.messaging.core.unitofwork.ProcessingContext;
        import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
        import org.axonframework.messaging.eventhandling.annotation.EventHandler;
        import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
        import org.springframework.boot.builder.SpringApplicationBuilder;
        import org.springframework.boot.test.context.TestConfiguration;
        import org.springframework.context.ConfigurableApplicationContext;
        import org.springframework.context.annotation.Bean;
        import org.springframework.context.annotation.Import;
        import tools.jackson.databind.json.JsonMapper;
        import static org.junit.jupiter.api.Assertions.*;

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
                        "--spring.jpa.hibernate.ddl-auto=update", "--spring.main.banner-mode=off");
            }
            @SuppressWarnings("unchecked")
            static ReadModelStore<Object, __MODEL__> store(ConfigurableApplicationContext application) {
                return (ReadModelStore<Object, __MODEL__>) application.getBean("__QUALIFIER__", ReadModelStore.class);
            }
            static UnitOfWorkFactory units(ConfigurableApplicationContext application) {
                return application.getBean(Configuration.class).getComponent(UnitOfWorkFactory.class);
            }
            static void append(ConfigurableApplicationContext application, Object event) throws Exception {
                units(application).create().executeWithResult(context -> {
                    EventAppender.forContext(context).append(event);
                    return CompletableFuture.<Void>completedFuture(null);
                }).get(10, TimeUnit.SECONDS);
            }
            static List<__MODEL__> expected() {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = new __PROJECTOR__(store);
        __EXPECTED__
                assertFalse(store.all().isEmpty(), "Proof must construct nonempty modeled rows");
                return store.all();
            }
            static List<__MODEL__> next(int replays) {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = new __PROJECTOR__(store);
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
                var deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(20);
                while (!condition.getAsBoolean() && System.nanoTime() < deadline) Thread.sleep(25);
                assertTrue(condition.getAsBoolean(), "Persistent processor/query condition timed out");
            }
            enum Failure { NONE, HANDLER, TOKEN, ASYNC, NON_JOINED }
            static volatile Failure failure = Failure.NONE;
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
                        new __PROJECTOR__(bound).on(event__ARGUMENT__);
                        throw new IllegalStateException("deliberate handler failure after view write");
                    }
                }
            }
            /** Real database rejection inside JpaTokenStore.storeToken, not a prepare-commit callback. */
            public static class TokenFailure implements org.h2.api.Trigger {
                private int tokenColumn;
                private int processorColumn;
                @Override public void init(java.sql.Connection connection, String schema, String trigger, String table,
                        boolean before, int type) throws java.sql.SQLException {
                    try (var statement = connection.createStatement(); var result = statement.executeQuery("select * from token_entry where 1=0")) {
                        for (var index = 1; index <= result.getMetaData().getColumnCount(); index++) {
                            var name = result.getMetaData().getColumnName(index);
                            if (name.equalsIgnoreCase("token")) tokenColumn = index - 1;
                            if (name.equalsIgnoreCase("processor_name")) processorColumn = index - 1;
                        }
                    }
                }
                @Override public void fire(java.sql.Connection connection, Object[] before, Object[] after) throws java.sql.SQLException {
                    if (failure == Failure.TOKEN && "__PACKAGE__".equals(after[processorColumn]) &&
                            !java.util.Arrays.equals(bytes(before[tokenColumn]), bytes(after[tokenColumn]))) {
                        tokenRejected.countDown();
                        throw new java.sql.SQLException("deliberate TokenStore persistence failure", "23514");
                    }
                }
                private byte[] bytes(Object value) throws java.sql.SQLException {
                    return value instanceof java.sql.Blob blob ? blob.getBytes(1, Math.toIntExact(blob.length())) : (byte[]) value;
                }
            }
            static void installTokenFailure(Path path) throws Exception {
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
