package io.cratis.stage.axon.runtime.views;

import org.axonframework.common.configuration.Configuration;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import io.cratis.stage.axon.runtime.ReadModelStore;
import jakarta.persistence.EntityManagerFactory;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.TimeUnit;
import java.util.function.Consumer;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;
import org.springframework.context.ConfigurableApplicationContext;
import tools.jackson.databind.json.JsonMapper;

import static org.junit.jupiter.api.Assertions.*;

public class JpaReadModelStoreTest {
    private static String database() { return "jdbc:h2:mem:" + UUID.randomUUID() + ";DB_CLOSE_DELAY=-1"; }
    private static ConfigurableApplicationContext start(String database) {
        var application = ViewFixtureTest.start(database);
        PersistentViewBootstrap.verify(application.getBean(EntityManagerFactory.class), "corpusviewviews", "compatible");
        return application;
    }
    private static UnitOfWorkFactory units(ConfigurableApplicationContext application) {
        return application.getBean(Configuration.class).getComponent(UnitOfWorkFactory.class);
    }
    public record Key(String value) {}
    public record Snapshot(List<String> values) {}

    @Test
    void durableStoreContractIsIsolatedAtomicAndFailsClosed() throws Exception {
        try (var application = start(database())) {
            var factory = application.getBean(EntityManagerFactory.class);
            var mapper = application.getBean(JsonMapper.class);
            var left = new JpaReadModelStore<Snapshot>(factory, mapper, "contract-left", Snapshot.class);
            var right = new JpaReadModelStore<Snapshot>(factory, mapper, "contract-right", Snapshot.class);
            var key = new Key("same");
            var value = new Snapshot(new ArrayList<>(List.of("original")));
            assertTrue(left.get(key).isEmpty());
            assertTrue(left.all().isEmpty());
            assertThrows(IllegalStateException.class, () -> left.put(key, value));
            assertThrows(IllegalStateException.class, () -> left.remove(key));
            assertThrows(IllegalStateException.class, left::clear);
            assertThrows(IllegalStateException.class, () -> left.update(key, ignored -> value));
            var missing = JpaReadModelStore.bind(left, Mockito.mock(ProcessingContext.class));
            assertThrows(IllegalStateException.class, () -> missing.put(key, value));
            write(application, left, bound -> {
                assertEquals(value, bound.update(key, current -> { assertNull(current); return value; }));
            });
            write(application, left, bound -> {
                var refusal = CompletableFuture.runAsync(() -> bound.put(key, value));
                var failure = assertThrows(CompletionException.class, refusal::join);
                assertInstanceOf(IllegalStateException.class, failure.getCause());
                assertTrue(failure.getCause().getMessage().contains("handler thread"));
            });
            try (var manager = factory.createEntityManager()) {
                assertEquals("contract-left:" + Key.class.getName() + ":{\"value\":\"same\"}",
                        manager.createQuery("select e.identity from StageViewRow e where e.namespace = 'contract-left'", String.class).getSingleResult());
            }
            value.values().add("outside");
            assertEquals(List.of("original"), left.get(new Key("same")).orElseThrow().values());
            var returned = left.get(key).orElseThrow();
            returned.values().add("detached");
            assertEquals(List.of("original"), left.get(key).orElseThrow().values());
            assertTrue(right.all().isEmpty(), "Namespaces must not leak");
            assertThrows(Exception.class, () -> write(application, left, bound -> bound.update(key, current -> {
                current.values().add("failed");
                throw new IllegalArgumentException("callback failed");
            })));
            assertEquals(List.of("original"), left.get(key).orElseThrow().values());
            var uuid = UUID.randomUUID();
            write(application, left, bound -> bound.put(uuid, new Snapshot(List.of("uuid"))));
            assertEquals(List.of("uuid"), left.get(UUID.fromString(uuid.toString())).orElseThrow().values());
            write(application, right, bound -> bound.put(key, new Snapshot(List.of("right"))));
            write(application, left, bound -> assertNull(bound.update(key, current -> null)));
            assertTrue(left.get(key).isEmpty());
            write(application, left, bound -> bound.remove(uuid));
            assertTrue(left.all().isEmpty());
            write(application, left, bound -> bound.put(key, new Snapshot(List.of("again"))));
            write(application, left, ReadModelStore::clear);
            assertTrue(left.all().isEmpty());
            assertEquals(List.of("right"), right.get(key).orElseThrow().values());
            var before = right.all();
            assertThrows(IllegalStateException.class, () -> PersistentViewBootstrap.verify(factory, "corpusviewviews", "incompatible"));
            assertEquals(before, right.all(), "Incompatible generation must not rebuild or clear rows");
        }
    }
    @Test
    void partKeyIdentityIsIndependentOfEntryOrderForEveryStoreOperation() throws Exception {
        try (var application = start(database())) {
            var parts = new JpaReadModelStore<Snapshot>(application.getBean(EntityManagerFactory.class),
                    application.getBean(JsonMapper.class), "contract-parts", Snapshot.class);
            var forward = new LinkedHashMap<String, Object>();
            forward.put("region", "west"); forward.put("period", "2");
            var reversed = new LinkedHashMap<String, Object>();
            reversed.put("period", "2"); reversed.put("region", "west");
            var other = Map.of("region", "east", "period", "2");
            write(application, parts, bound -> {
                bound.put(Map.of("period", "2", "region", "west"), new Snapshot(List.of("first")));
                bound.put(other, new Snapshot(List.of("other")));
            });
            assertEquals(List.of("first"), parts.get(reversed).orElseThrow().values());
            assertEquals(List.of("first"), parts.get(Map.of("period", "2", "region", "west")).orElseThrow().values(), "Map implementation must not change row identity");
            write(application, parts, bound -> bound.update(reversed, current -> {
                assertEquals(List.of("first"), current.values());
                return new Snapshot(List.of("updated"));
            }));
            assertEquals(2, parts.all().size(), "Reordering parts must not create an alias row");
            assertEquals(List.of("updated"), parts.get(forward).orElseThrow().values());
            assertTrue(parts.get(Map.of("region", "west", "period", "3")).isEmpty());
            try (var manager = application.getBean(EntityManagerFactory.class).createEntityManager()) {
                assertTrue(manager.createQuery("select e.identity from StageViewRow e where e.namespace = 'contract-parts'", String.class)
                        .getResultList().contains("contract-parts:java.util.Map:{\"period\":\"2\",\"region\":\"west\"}"));
            }
            assertThrows(IllegalArgumentException.class, () -> parts.get(Map.of(1, "x")));
            assertThrows(IllegalArgumentException.class, () -> parts.get(Map.of("1", 1)));
            assertThrows(IllegalArgumentException.class, () -> parts.get(Map.of("nested", Map.of("x", "y"))));
            assertThrows(IllegalArgumentException.class, () -> parts.get(Map.of()));
            write(application, parts, bound -> bound.remove(reversed));
            assertTrue(parts.get(forward).isEmpty());
            assertEquals(List.of("other"), parts.get(other).orElseThrow().values());
        }
    }
    private static void write(ConfigurableApplicationContext application, ReadModelStore<Object, Snapshot> store,
            Consumer<ReadModelStore<Object, Snapshot>> action) throws Exception {
        units(application).create().executeWithResult(context -> {
            action.accept(JpaReadModelStore.bind(store, context));
            return CompletableFuture.<Void>completedFuture(null);
        }).get(10, TimeUnit.SECONDS);
    }
}
