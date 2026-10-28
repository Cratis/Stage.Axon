package io.cratis.stage.axon.runtime;

import java.util.concurrent.CompletableFuture;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;

class InMemoryUniqueValueStoreTest extends UniqueValueStoreContractTest {
    private final UniqueValueStore values = new InMemoryUniqueValueStore();

    @Override protected UniqueValueStore store() { return values; }

    @Test
    void rejectsAContenderBeforeTheHolderCompletes() {
        var finish = new CompletableFuture<Void>();
        var holder = factory().create().executeWithResult(context -> {
            values.occurOnce(context, "pending", "owner", "once");
            return finish;
        });
        try {
            assertFalse(holder.isDone());
            var failure = assertThrows(Exception.class, () -> run(context -> values.occurOnce(context, "pending", "owner", "once")));
            assertInstanceOf(UniqueValueConflict.class, failure.getCause());
        } finally { finish.complete(null); }
        holder.join();
    }
}
