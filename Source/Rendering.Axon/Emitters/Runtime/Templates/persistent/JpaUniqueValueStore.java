// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.persistent;

import java.util.List;
import jakarta.persistence.EntityManager;
import jakarta.persistence.EntityManagerFactory;
import org.axonframework.common.function.ThrowingConsumer;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.transaction.jpa.JpaTransactionalExecutorProvider;
import __RUNTIME__.UniqueValueConflict;
import __RUNTIME__.UniqueValueStore;
import __RUNTIME__.UniqueValues;

public final class JpaUniqueValueStore implements UniqueValueStore {
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
        execute(context, manager -> {
            var value = UniqueValues.key(values, ignoreCasing);
            var key = constraint.length() + ":" + constraint + value;
            var identity = UniqueValues.owner(owner);
            var existing = manager.find(UniqueClaim.class, key);
            if (existing != null && !existing.owner().equals(identity)) throw new UniqueValueConflict(message);
            removeOwner(manager, constraint, identity, existing);
            if (existing == null && !value.isEmpty()) {
                manager.persist(new UniqueClaim(key, constraint, identity));
                try { manager.flush(); }
                catch (jakarta.persistence.PersistenceException failure) {
                    if (isDuplicate(failure)) throw new UniqueValueConflict(message);
                    throw failure;
                }
            }
        });
    }

    @Override
    public void releaseOwner(ProcessingContext context, String constraint, Object owner) {
        execute(context, manager -> removeOwner(manager, constraint, UniqueValues.owner(owner), null));
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

    private static boolean isDuplicate(Throwable failure) {
        for (var cause = failure; cause != null; cause = cause.getCause())
            if (cause instanceof java.sql.SQLException sql && "23505".equals(sql.getSQLState())) return true;
        return false;
    }
}
