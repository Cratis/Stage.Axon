package io.cratis.stage.axon.runtime.persistent;

import io.cratis.stage.axon.runtime.UniqueValueConflict;
import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.UniqueValues;
import jakarta.persistence.EntityManager;
import jakarta.persistence.EntityManagerFactory;
import jakarta.persistence.LockTimeoutException;
import jakarta.persistence.PessimisticLockException;
import java.sql.SQLException;
import java.util.List;
import java.util.Set;
import org.axonframework.common.function.ThrowingConsumer;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;

public final class JpaUniqueValueStore implements UniqueValueStore {
    private static final Set<String> CONFLICT_STATES = Set.of("23000", "23505", "50200", "HYT00", "HYT01", "55P03", "40P01", "40001");
    private final JpaTransactionalExecutorProvider transactions;
    public JpaUniqueValueStore(EntityManagerFactory factory) { transactions = new JpaTransactionalExecutorProvider(factory); }

    @Override
    public boolean claim(String constraint, Object value, Object owner) {
        throw new UnsupportedOperationException("Persistent uniqueness requires the append ProcessingContext");
    }

    @Override
    public void release(String constraint, Object value, Object owner) {
        throw new UnsupportedOperationException("Persistent uniqueness requires the append ProcessingContext");
    }

    @Override
    public void assign(ProcessingContext context, String constraint, List<?> values, Object owner, boolean ignoreCasing, String message) {
        execute(context, message, manager -> {
            var value = UniqueValues.key(values, ignoreCasing);
            var key = constraint.length() + ":" + constraint + value;
            var identity = UniqueValues.owner(owner);
            var existing = manager.find(UniqueClaim.class, key);
            if (existing != null && !existing.owner().equals(identity)) throw new UniqueValueConflict(message);
            removeOwner(manager, constraint, identity, existing);
            if (existing == null && !value.isEmpty()) {
                manager.persist(new UniqueClaim(key, constraint, identity));
                manager.flush();
            }
        });
    }

    @Override
    public void occurOnce(ProcessingContext context, String constraint, Object owner, String message) {
        execute(context, message, manager -> {
            var identity = UniqueValues.owner(owner);
            var key = constraint.length() + ":" + constraint + identity;
            if (manager.find(UniqueClaim.class, key) != null) throw new UniqueValueConflict(message);
            // Flush a preceding release before inserting the same primary key again.
            manager.flush();
            manager.persist(new UniqueClaim(key, constraint, identity));
            manager.flush();
        });
    }

    @Override
    public void releaseOwner(ProcessingContext context, String constraint, Object owner) {
        execute(context, manager -> removeOwner(manager, constraint, UniqueValues.owner(owner), null));
    }

    private void execute(ProcessingContext context, String message, ThrowingConsumer<EntityManager, Exception> action) {
        try { execute(context, action); }
        catch (RuntimeException failure) {
            if (isConflict(failure)) throw new UniqueValueConflict(message);
            throw failure;
        }
    }

    private void execute(ProcessingContext context, ThrowingConsumer<EntityManager, Exception> action) {
        var executor = transactions.findTransactionalExecutor(context);
        if (executor == null) throw new IllegalStateException("No JPA transaction enlisted in the append ProcessingContext");
        executor.accept(action).join();
    }

    private static void removeOwner(EntityManager manager, String constraint, String owner, UniqueClaim retained) {
        var previous = manager.createQuery("select c from UniqueClaim c where c.constraintName = :constraint and c.owner = :owner", UniqueClaim.class)
                .setParameter("constraint", constraint).setParameter("owner", owner).getResultList();
        previous.stream().filter(claim -> claim != retained).forEach(manager::remove);
    }

    static boolean isConflict(Throwable failure) {
        for (var cause = failure; cause != null; cause = cause.getCause()) {
            if (cause instanceof LockTimeoutException || cause instanceof PessimisticLockException) return true;
            if (cause instanceof SQLException sql && isConflictState(sql)) return true;
        }
        return false;
    }

    private static boolean isConflictState(SQLException failure) {
        for (var sql = failure; sql != null; sql = sql.getNextException())
            if (sql.getSQLState() != null && CONFLICT_STATES.contains(sql.getSQLState())) return true;
        return false;
    }
}
