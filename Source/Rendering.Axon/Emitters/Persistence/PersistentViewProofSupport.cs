// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewProofSupport
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

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
        import java.util.concurrent.atomic.AtomicBoolean;
        import java.util.function.BooleanSupplier;
        import org.axonframework.common.configuration.Configuration;
        import org.axonframework.eventsourcing.eventstore.EventAppender;
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
            static ConfigurableApplicationContext start(Path path) {
                return new SpringApplicationBuilder(FlatFixture.class).profiles("persistent").run(
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
            static List<__MODEL__> next(List<__MODEL__> previous) {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = new __PROJECTOR__(store);
                // Reconstruct the same key by replaying the modeled arrangement, not by guessing a row property.
        __EXPECTED__
                projector.on(__EVENT____ARGUMENT__);
                if (!new java.util.HashSet<>(store.all()).equals(new java.util.HashSet<>(previous))) return store.all();
                projector.on(__EVENT____ARGUMENT__);
                return store.all();
            }
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
            static final AtomicBoolean fail = new AtomicBoolean();
            static volatile CountDownLatch rolledBack = new CountDownLatch(1);
            public static class RollbackWitness {
                @EventHandler
                public void handle(Object event, ProcessingContext context) {
                    if (!fail.get()) return;
                    context.runOnPrepareCommit((java.util.function.Consumer<ProcessingContext>) ignored -> { throw new IllegalStateException("deliberate processor rollback"); });
                    context.onError((ignored, phase, failure) -> rolledBack.countDown());
                }
            }
            /** Scan-free imports of the real emitted flat projector, query and persistence infrastructure. */
            @TestConfiguration(proxyBeanMethods = false)
            @EnableAutoConfiguration
            @Import({ __IMPORTS__ })
            static class FlatFixture {
                @Bean RollbackWitness rollbackWitness() { return new RollbackWitness(); }
            }
        }
        """;
}
