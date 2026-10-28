package io.cratis.stage.axon.runtime;

import java.util.List;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.assertThrows;

class UniqueValueStoreDefaultTest {
    @Test
    void customStoresWithoutOccurrenceSupportFailClosed() {
        var store = new UniqueValueStore() {
            @Override public boolean claim(String constraint, Object value, Object owner) { return true; }
            @Override public void release(String constraint, Object value, Object owner) {}
            @Override public void assign(ProcessingContext context, String constraint, List<?> values, Object owner, boolean ignoreCasing, String message) {}
            @Override public void releaseOwner(ProcessingContext context, String constraint, Object owner) {}
        };
        assertThrows(UnsupportedOperationException.class, () -> store.occurOnce(null, "once", "owner", "conflict"));
    }
}
