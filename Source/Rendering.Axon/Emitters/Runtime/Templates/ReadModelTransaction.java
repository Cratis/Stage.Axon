package __RUNTIME__;

import java.lang.ref.ReferenceQueue;
import java.lang.ref.WeakReference;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import java.util.function.UnaryOperator;
import org.axonframework.messaging.core.Context.ResourceKey;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.ProcessingLifecycle.DefaultPhases;
import org.axonframework.messaging.core.unitofwork.ProcessingLifecycle.Phase;

/** Stages flat projector writes; direct, unbound store calls remain auto-commit. */
public final class ReadModelTransaction {
    private static final Phase APPLY = () -> DefaultPhases.AFTER_COMMIT.order() - 1;
    private static final long WAIT_NANOS = TimeUnit.SECONDS.toNanos(10);
    private static final ReferenceQueue<ReadModelStore<?, ?>> COLLECTED = new ReferenceQueue<>();
    private static final Map<StoreIdentity, Coordinator> COORDINATORS = new HashMap<>();

    private ReadModelTransaction() { }

    public static <K, V> ReadModelStore<K, V> bind(ReadModelStore<K, V> store, ProcessingContext context) {
        Objects.requireNonNull(context, "A processing context is required for read model writes");
        var coordinator = coordinator(Objects.requireNonNull(store));
        return context.computeResourceIfAbsent(coordinator.<K, V>resource(), () -> {
            var changes = new BoundStore<>(store, coordinator);
            // Axon skips later phases after a failure, so reaching APPLY means every earlier phase succeeded.
            context.runOn(APPLY, completed -> changes.commit());
            context.doFinally(completed -> coordinator.release(changes));
            return changes;
        });
    }

    private static synchronized Coordinator coordinator(ReadModelStore<?, ?> store) {
        for (var key = COLLECTED.poll(); key != null; key = COLLECTED.poll()) COORDINATORS.remove(key);
        return COORDINATORS.computeIfAbsent(new StoreIdentity(store), ignored -> new Coordinator());
    }

    private static final class StoreIdentity extends WeakReference<ReadModelStore<?, ?>> {
        private final int hash;
        StoreIdentity(ReadModelStore<?, ?> store) { super(store, COLLECTED); hash = System.identityHashCode(store); }
        @Override public int hashCode() { return hash; }
        @Override public boolean equals(Object other) {
            return this == other || other instanceof StoreIdentity key && get() != null && get() == key.get();
        }
    }

    private static final class Coordinator {
        private final ResourceKey<BoundStore<?, ?>> resource = ResourceKey.withLabel("stage-read-model-" + UUID.randomUUID());
        private final Map<Object, Object> reservations = new HashMap<>();

        @SuppressWarnings({"unchecked", "rawtypes"})
        <K, V> ResourceKey<BoundStore<K, V>> resource() { return (ResourceKey) resource; }

        synchronized void reserve(Object key, Object owner) {
            var deadline = System.nanoTime() + WAIT_NANOS;
            while (reservations.containsKey(key) && reservations.get(key) != owner) {
                var remaining = deadline - System.nanoTime();
                if (remaining <= 0) throw new IllegalStateException("Read model key is being changed by another unit of work");
                try { TimeUnit.NANOSECONDS.timedWait(this, remaining); }
                catch (InterruptedException interrupted) {
                    Thread.currentThread().interrupt();
                    throw new IllegalStateException("Interrupted waiting for a read model key", interrupted);
                }
            }
            reservations.put(key, owner);
        }

        synchronized void release(Object owner) {
            reservations.entrySet().removeIf(entry -> entry.getValue() == owner);
            notifyAll();
        }
    }

    private static final class BoundStore<K, V> implements ReadModelStore<K, V> {
        private final ReadModelStore<K, V> store;
        private final Coordinator coordinator;
        private final Map<K, Optional<V>> staged = new LinkedHashMap<>();

        BoundStore(ReadModelStore<K, V> store, Coordinator coordinator) { this.store = store; this.coordinator = coordinator; }

        @Override public Optional<V> get(K key) {
            coordinator.reserve(key, this);
            return staged.containsKey(key) ? staged.get(key) : store.get(key);
        }

        @Override public void put(K key, V value) {
            coordinator.reserve(key, this);
            staged.put(key, Optional.of(value));
        }

        @Override public void remove(K key) {
            coordinator.reserve(key, this);
            staged.put(key, Optional.empty());
        }

        @Override public V update(K key, UnaryOperator<V> update) {
            var next = update.apply(get(key).orElse(null));
            if (next == null) remove(key);
            else put(key, next);
            return next;
        }

        @Override public List<V> all() { throw new UnsupportedOperationException("Transactional read model scans are not supported"); }
        @Override public void clear() { throw new UnsupportedOperationException("Transactional read model clears are not supported"); }

        void commit() {
            staged.forEach((key, value) -> {
                if (value.isPresent()) store.put(key, value.get());
                else store.remove(key);
            });
        }
    }
}
