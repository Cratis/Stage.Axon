// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewContractTests
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import __PERSISTENCE__.JpaReadModelStore;
        import __PERSISTENCE__.PersistentViewBootstrap;
        import __RUNTIME__.ReadModelStore;
        import jakarta.persistence.EntityManagerFactory;
        import java.util.ArrayList;
        import java.util.List;
        import java.util.UUID;
        import java.util.concurrent.CompletableFuture;
        import java.util.concurrent.TimeUnit;
        import java.util.function.Consumer;
        import org.axonframework.messaging.core.unitofwork.ProcessingContext;
        import org.junit.jupiter.api.Test;
        import org.springframework.context.ConfigurableApplicationContext;
        import tools.jackson.databind.json.JsonMapper;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;

        public class __NAME__StoreTest {
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
                    var missing = JpaReadModelStore.bind(left, org.mockito.Mockito.mock(ProcessingContext.class));
                    assertThrows(IllegalStateException.class, () -> missing.put(key, value));
                    write(application, left, bound -> {
                        assertEquals(value, bound.update(key, current -> { assertNull(current); return value; }));
                    });
                    write(application, left, bound -> {
                        var refusal = CompletableFuture.runAsync(() -> bound.put(key, value));
                        var failure = assertThrows(java.util.concurrent.CompletionException.class, refusal::join);
                        assertInstanceOf(IllegalStateException.class, failure.getCause());
                        assertTrue(failure.getCause().getMessage().contains("handler thread"));
                    });
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
                    assertThrows(IllegalStateException.class, () -> PersistentViewBootstrap.verify(factory, "__APPLICATION__", "incompatible"));
                    assertEquals(before, right.all(), "Incompatible generation must not rebuild or clear rows");
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
        """;
}
