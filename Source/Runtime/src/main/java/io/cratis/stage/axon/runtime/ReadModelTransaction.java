package io.cratis.stage.axon.runtime;

import java.lang.ref.ReferenceQueue;
import java.lang.ref.WeakReference;
import java.util.Collection;
import java.util.HashMap;
import java.util.IdentityHashMap;
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
    private static final Object MONITOR = new Object();
    private static final ResourceKey<Object> OWNER = ResourceKey.withLabel("stage-read-model-owner");
    private static final Map<Object, Reservation> WAITING = new IdentityHashMap<>();
    private static final ReferenceQueue<ReadModelStore<?, ?>> COLLECTED = new ReferenceQueue<>();
    private static final Map<StoreIdentity, Coordinator> COORDINATORS = new HashMap<>();

    private ReadModelTransaction() { }

    public static <K, V> ReadModelStore<K, V> bind(ReadModelStore<K, V> store, ProcessingContext context) {
        Objects.requireNonNull(context, "A processing context is required for read model writes");
        var coordinator = coordinator(Objects.requireNonNull(store));
        // Resolved outside the supplier: context resources must not be computed recursively.
        var owner = context.computeResourceIfAbsent(OWNER, Object::new);
        return context.computeResourceIfAbsent(coordinator.<K, V>resource(), () -> {
            var changes = new BoundStore<>(store, coordinator, owner);
            // Axon skips later phases after a failure, so reaching APPLY means every earlier phase succeeded.
            context.runOn(APPLY, completed -> changes.commit());
            context.doFinally(completed -> changes.release());
            return changes;
        });
    }

    public static void reserve(ReadModelStore<?, ?> boundStore, Collection<?> keys) {
        if (boundStore instanceof BoundStore<?, ?> bound) {
            keys.stream().distinct().sorted(ReadModelTransaction::compareKeys).forEach(bound::reserve);
        }
    }

    @SuppressWarnings({"unchecked", "rawtypes"})
    private static int compareKeys(Object left, Object right) {
        if (left.getClass() == right.getClass() && left instanceof Comparable comparable) return comparable.compareTo(right);
        var type = left.getClass().getName().compareTo(right.getClass().getName());
        if (type != 0) return type;
        var text = String.valueOf(left).compareTo(String.valueOf(right));
        return text != 0 ? text : Integer.compare(left.hashCode(), right.hashCode());
    }

    private static Coordinator coordinator(ReadModelStore<?, ?> store) {
        synchronized (MONITOR) {
            for (var key = COLLECTED.poll(); key != null; key = COLLECTED.poll()) COORDINATORS.remove(key);
            return COORDINATORS.computeIfAbsent(new StoreIdentity(store), ignored -> new Coordinator());
        }
    }

    private static void checkCycle(Object owner, Object blocker) {
        for (var waiting = blocker; waiting != null;) {
            if (waiting == owner) throw new IllegalStateException("Read model keys are locked in a cycle with another unit of work");
            var reservation = WAITING.get(waiting);
            waiting = reservation == null ? null : reservation.coordinator.reservations.get(reservation.key);
        }
    }

    private record Reservation(Coordinator coordinator, Object key) { }

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

        void reserve(Object key, Object owner) {
            synchronized (MONITOR) {
                if (reservations.get(key) == owner) return;
                var deadline = System.nanoTime() + WAIT_NANOS;
                try {
                    while (reservations.containsKey(key) && reservations.get(key) != owner) {
                        var blocker = reservations.get(key);
                        checkCycle(owner, blocker);
                        WAITING.put(owner, new Reservation(this, key));
                        var remaining = deadline - System.nanoTime();
                        if (remaining <= 0) throw new IllegalStateException("Read model key is being changed by another unit of work");
                        try { TimeUnit.NANOSECONDS.timedWait(MONITOR, remaining); }
                        catch (InterruptedException interrupted) {
                            Thread.currentThread().interrupt();
                            throw new IllegalStateException("Interrupted waiting for a read model key", interrupted);
                        }
                    }
                    reservations.put(key, owner);
                } finally { WAITING.remove(owner); }
            }
        }

        void release(Object owner) {
            synchronized (MONITOR) {
                reservations.entrySet().removeIf(entry -> entry.getValue() == owner);
                MONITOR.notifyAll();
            }
        }
    }

    private static final class BoundStore<K, V> implements ReadModelStore<K, V> {
        private final ReadModelStore<K, V> store;
        private final Coordinator coordinator;
        private final Object owner;
        private final Map<K, Optional<V>> staged = new LinkedHashMap<>();

        BoundStore(ReadModelStore<K, V> store, Coordinator coordinator, Object owner) {
            this.store = store;
            this.coordinator = coordinator;
            this.owner = owner;
        }

        void reserve(Object key) { coordinator.reserve(key, owner); }
        void release() { coordinator.release(owner); }

        @Override public Optional<V> get(K key) {
            reserve(key);
            return staged.containsKey(key) ? staged.get(key) : store.get(key);
        }

        @Override public void put(K key, V value) {
            reserve(key);
            staged.put(key, Optional.of(value));
        }

        @Override public void remove(K key) {
            reserve(key);
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
            release();
        }
    }
}
