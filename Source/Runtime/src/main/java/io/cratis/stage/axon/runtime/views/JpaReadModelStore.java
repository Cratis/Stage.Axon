package io.cratis.stage.axon.runtime.views;

import io.cratis.stage.axon.runtime.ReadModelStore;
import jakarta.persistence.EntityManager;
import jakarta.persistence.EntityManagerFactory;
import jakarta.persistence.LockModeType;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Optional;
import java.util.TreeMap;
import java.util.function.Function;
import java.util.function.UnaryOperator;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;
import tools.jackson.databind.json.JsonMapper;

/** Detached JSON rows; writes require the processor's enlisted JPA executor. */
public final class JpaReadModelStore<V> implements ReadModelStore<Object, V> {
    private final EntityManagerFactory factory;
    private final JsonMapper mapper;
    private final String namespace;
    private final Class<V> type;
    private final ProcessingContext context;
    private final Thread handlerThread;

    public JpaReadModelStore(EntityManagerFactory factory, JsonMapper mapper, String namespace, Class<V> type) {
        this(factory, mapper, namespace, type, null);
    }
    private JpaReadModelStore(EntityManagerFactory factory, JsonMapper mapper, String namespace, Class<V> type, ProcessingContext context) {
        this.factory = factory;
        this.mapper = mapper;
        this.namespace = namespace;
        this.type = type;
        this.context = context;
        this.handlerThread = context == null ? null : Thread.currentThread();
    }
    public static <V> ReadModelStore<Object, V> bind(ReadModelStore<Object, V> store, ProcessingContext context) {
        if (store instanceof JpaReadModelStore<V> persistent) {
            Objects.requireNonNull(context, "Persistent views require the processor ProcessingContext");
            return new JpaReadModelStore<>(persistent.factory, persistent.mapper, persistent.namespace, persistent.type, context);
        }
        return store;
    }
    @Override public Optional<V> get(Object key) {
        return key == null ? Optional.empty() : read(manager -> Optional.ofNullable(decode(find(manager, key, false))));
    }
    @Override public List<V> all() {
        return read(manager -> manager.createQuery("select r from StageViewRow r where r.namespace = :namespace", ViewRow.class)
                .setParameter("namespace", namespace).getResultList().stream().map(this::decode).toList());
    }
    @Override public void put(Object key, V value) {
        Objects.requireNonNull(value);
        update(key, ignored -> value);
    }
    @Override public void remove(Object key) {
        if (key == null) write(manager -> null);
        else update(key, ignored -> null);
    }
    @Override public void clear() {
        write(manager -> {
            manager.createQuery("select r from StageViewRow r where r.namespace = :namespace", ViewRow.class)
                    .setParameter("namespace", namespace).setLockMode(LockModeType.PESSIMISTIC_WRITE)
                    .getResultList().forEach(manager::remove);
            manager.flush();
            return null;
        });
    }
    @Override public V update(Object key, UnaryOperator<V> update) {
        return write(manager -> {
            var row = find(manager, key, true);
            // Decode first: a failing/mutating callback cannot modify a managed row.
            var next = update.apply(decode(row));
            if (next == null) {
                if (row != null) manager.remove(row);
            } else {
                var payload = mapper.writeValueAsString(next);
                if (row == null) manager.persist(new ViewRow(id(key), namespace, identity(key), payload));
                else row.payload(payload);
            }
            manager.flush();
            return next;
        });
    }
    private V decode(ViewRow row) { return row == null ? null : mapper.readValue(row.payload(), type); }
    private ViewRow find(EntityManager manager, Object key, boolean lock) {
        var row = manager.find(ViewRow.class, id(key), lock ? LockModeType.PESSIMISTIC_WRITE : LockModeType.NONE);
        if (row != null && !row.identity().equals(identity(key))) throw new IllegalStateException("Persistent view key digest collision");
        return row;
    }
    private String identity(Object key) {
        Objects.requireNonNull(key);
        Object value = key;
        var kind = key.getClass().getName();
        if (key instanceof Map<?, ?> map) {
            if (map.isEmpty() || map.entrySet().stream().anyMatch(entry -> !(entry.getKey() instanceof String) || !(entry.getValue() instanceof String)))
                throw new IllegalArgumentException("Persistent part keys require nonempty string keys and values");
            value = new TreeMap<>(map);
            kind = Map.class.getName();
        }
        return namespace + ":" + kind + ":" + mapper.writeValueAsString(value);
    }
    private String id(Object key) { return digest(identity(key)); }
    static String digest(String value) {
        try { return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8))); }
        catch (NoSuchAlgorithmException failure) { throw new IllegalStateException(failure); }
    }
    private <T> T write(Function<EntityManager, T> action) {
        if (context == null) throw new IllegalStateException("Persistent view writes require the processor ProcessingContext");
        if (Thread.currentThread() != handlerThread) throw new IllegalStateException("Persistent view writes must stay on the handler thread");
        var executor = new JpaTransactionalExecutorProvider(factory).findTransactionalExecutor(context);
        if (executor == null) throw new IllegalStateException("No JPA transaction enlisted in the processor ProcessingContext");
        return executor.apply(manager -> {
            if (!manager.isJoinedToTransaction() || !manager.getEntityManagerFactory().unwrap(EntityManagerFactory.class)
                    .equals(factory.unwrap(EntityManagerFactory.class)))
                throw new IllegalStateException("Persistent views require the processor's active transaction on the configured database");
            return action.apply(manager);
        }).join();
    }
    private <T> T read(Function<EntityManager, T> action) {
        if (context != null) return write(action);
        // Contextless query reads never create a write transaction.
        try (var manager = factory.createEntityManager()) { return action.apply(manager); }
    }
}
