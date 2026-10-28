package io.cratis.stage.axon.runtime.persistent;

import jakarta.persistence.LockTimeoutException;
import jakarta.persistence.PersistenceException;
import jakarta.persistence.PessimisticLockException;
import java.sql.SQLException;
import java.util.concurrent.CompletionException;
import java.util.stream.Stream;
import org.hibernate.exception.LockAcquisitionException;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.MethodSource;
import org.junit.jupiter.params.provider.NullSource;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.dao.CannotAcquireLockException;
import org.springframework.dao.PessimisticLockingFailureException;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class JpaUniqueValueConflictTest {
    @ParameterizedTest
    @ValueSource(strings = {"23000", "23505", "50200", "HYT00", "HYT01", "55P03", "40P01", "40001"})
    void recognizesConflictingSqlStatesThroughCauseChains(String state) {
        var sql = new SQLException("contender", state);
        assertTrue(JpaUniqueValueStore.isConflict(new CompletionException(new PersistenceException(sql))));
    }

    @ParameterizedTest
    @NullSource
    @ValueSource(strings = {"08006", "23502", "42000"})
    void doesNotHideUnrelatedFailures(String state) {
        assertFalse(JpaUniqueValueStore.isConflict(new PersistenceException(new SQLException("unrelated", state))));
    }

    @ParameterizedTest
    @MethodSource("lockingFailures")
    void recognizesProviderAndSpringWrappers(Throwable failure) {
        assertTrue(JpaUniqueValueStore.isConflict(new CompletionException(failure)));
    }

    static Stream<Throwable> lockingFailures() {
        return Stream.of(
                new LockTimeoutException("timeout"),
                new PessimisticLockException("deadlock"),
                new PessimisticLockingFailureException("deadlock", new SQLException("deadlock", "40P01")),
                new CannotAcquireLockException("timeout", new LockTimeoutException("timeout")),
                new LockAcquisitionException("deadlock", new SQLException("deadlock", "40001")));
    }

    @Test
    void followsChainedSqlExceptions() {
        var sql = new SQLException("batch", (String) null);
        sql.setNextException(new SQLException("duplicate", "23000"));
        assertTrue(JpaUniqueValueStore.isConflict(new PersistenceException(sql)));
    }
}
