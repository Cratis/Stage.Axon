package io.cratis.stage.axon.runtime;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.stream.IntStream;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class InMemoryReadModelStoreTest {
    @Test
    void storeUpdatesAreAtomicAndListsAreSnapshots() throws Exception {
        var store = new InMemoryReadModelStore<String, Integer>();
        try (var executor = Executors.newFixedThreadPool(4)) {
            var updates = IntStream.range(0, 100).mapToObj(ignored -> CompletableFuture.runAsync(() -> store.update("count", current -> current == null ? 1 : current + 1), executor)).toArray(CompletableFuture<?>[]::new);
            CompletableFuture.allOf(updates).get();
        }
        var snapshot = store.all();
        assertEquals(100, store.get("count").orElseThrow());
        store.clear();
        assertEquals(1, snapshot.size());
        assertTrue(store.all().isEmpty());
    }
}
