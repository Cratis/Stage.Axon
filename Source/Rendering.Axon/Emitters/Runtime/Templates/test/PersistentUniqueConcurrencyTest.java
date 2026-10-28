package __RUNTIME__;

import io.cratis.stage.axon.runtime.UniqueValueConflict;
import io.cratis.stage.axon.runtime.UniqueValueStore;
import io.cratis.stage.axon.runtime.UniqueValues;
import io.cratis.stage.axon.runtime.persistent.JpaUniqueValueStore;
import jakarta.persistence.EntityManagerFactory;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.CyclicBarrier;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.jpa.AggregateBasedJpaEventStorageEngine;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyBoolean;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.reset;

/** Inherited by a corpus proof that dispatches its generated commands through the gateway. */
@Timeout(60)
public abstract class PersistentUniqueConcurrencyTest {
    protected static final int CONTENDERS = 8;
    protected static final int REPETITIONS = 25;
    @Autowired private Configuration configuration;
    @Autowired private EntityManagerFactory factory;
    @MockitoSpyBean protected UniqueValueStore claims;
    private ExecutorService workers;

    protected abstract CompletableFuture<Void> register(UUID owner, String value);
    protected abstract CompletableFuture<Void> release(UUID owner);
    protected abstract long events(UUID owner) throws Exception;

    @BeforeEach
    void startContenders() {
        assertInstanceOf(JpaUniqueValueStore.class, claims);
        assertInstanceOf(AggregateBasedJpaEventStorageEngine.class, configuration.getComponent(EventStorageEngine.class));
        workers = Executors.newFixedThreadPool(CONTENDERS + 1);
    }

    @AfterEach
    void stopContenders() throws Exception {
        workers.shutdownNow();
        assertTrue(workers.awaitTermination(5, TimeUnit.SECONDS), "All contenders must terminate");
        reset(claims);
    }

    @Test
    void exactlyOneGeneratedCommandClaimsTheValue() throws Exception {
        for (var repetition = 0; repetition < REPETITIONS; repetition++) race(false);
    }

    @Test
    void caseFoldedGeneratedClaimsHaveOnlyOneWinner() throws Exception {
        for (var repetition = 0; repetition < REPETITIONS; repetition++) race(true);
    }

    @Test
    void failedGeneratedAppendRollsBackItsClaimWhileOthersContend() throws Exception {
        for (var repetition = 0; repetition < REPETITIONS; repetition++) {
            var value = UUID.randomUUID().toString();
            var failedOwner = UUID.randomUUID();
            var acquired = new CountDownLatch(1);
            var arrived = new CountDownLatch(CONTENDERS);
            reset(claims);
            doAnswer(invocation -> {
                var owner = invocation.getArgument(3).toString();
                if (!owner.equals(failedOwner.toString())) arrived.countDown();
                var result = invocation.callRealMethod();
                if (owner.equals(failedOwner.toString())) {
                    acquired.countDown();
                    assertTrue(arrived.await(5, TimeUnit.SECONDS), "Contenders must overlap the reserved claim");
                    throw new IllegalStateException("forced append rollback");
                }
                return result;
            }).when(claims).assign(any(), anyString(), anyList(), any(), anyBoolean(), anyString());
            var failed = CompletableFuture.runAsync(() -> {
                var failure = assertThrows(Exception.class, () -> register(failedOwner, value).get(10, TimeUnit.SECONDS));
                assertEquals("forced append rollback", root(failure).getMessage());
            }, workers);
            assertTrue(acquired.await(5, TimeUnit.SECONDS), "The failing generated command must reserve its claim");
            var owners = owners();
            var calls = owners.stream().map(owner -> CompletableFuture.supplyAsync(() -> claim(owner, value), workers)).toList();
            failed.get(10, TimeUnit.SECONDS);
            assertEquals(1, winners(calls));
            assertEquals(1, rows(value));
            assertEquals(0, events(failedOwner));
            assertEquals(1, eventCount(owners));
        }
    }

    @Test
    void generatedReleaseAndClaimsNeverCommitTwoOwners() throws Exception {
        for (var repetition = 0; repetition < REPETITIONS; repetition++) {
            var value = UUID.randomUUID().toString();
            var previous = UUID.randomUUID();
            register(previous, value).get(10, TimeUnit.SECONDS);
            var owners = owners();
            var gate = new CyclicBarrier(CONTENDERS + 1);
            var releasing = CompletableFuture.runAsync(() -> {
                await(gate);
                try { release(previous).get(10, TimeUnit.SECONDS); }
                catch (Exception failure) { throw new AssertionError("Generated release must commit", failure); }
            }, workers);
            var calls = owners.stream().map(owner -> CompletableFuture.supplyAsync(() -> {
                await(gate);
                return claim(owner, value);
            }, workers)).toList();
            var won = winners(calls);
            releasing.get(10, TimeUnit.SECONDS);
            assertTrue(won <= 1, "Concurrent release must never permit two winners");
            assertEquals(won, rows(value));
            assertEquals(won, eventCount(owners));
            if (won == 0) {
                assertTrue(claim(owners.getFirst(), value), "Committed release must permit reuse");
                assertEquals(1, rows(value));
                assertEquals(1, eventCount(owners));
            }
        }
    }

    private void race(boolean folded) throws Exception {
        var value = "claim-" + UUID.randomUUID();
        var owners = owners();
        var gate = new CyclicBarrier(CONTENDERS);
        var calls = new ArrayList<CompletableFuture<Boolean>>();
        for (var index = 0; index < CONTENDERS; index++) {
            var owner = owners.get(index);
            var candidate = folded && index % 2 == 0 ? value.toUpperCase(Locale.ROOT) : value;
            calls.add(CompletableFuture.supplyAsync(() -> {
                await(gate);
                return claim(owner, candidate);
            }, workers));
        }
        assertEquals(1, winners(calls));
        assertEquals(1, rows(value));
        assertEquals(1, eventCount(owners));
    }

    private boolean claim(UUID owner, String value) {
        try { register(owner, value).get(10, TimeUnit.SECONDS); return true; }
        catch (Exception failure) {
            assertInstanceOf(UniqueValueConflict.class, root(failure));
            assertEquals(0, uncheckedEvents(owner), "A losing generated command must append nothing");
            return false;
        }
    }

    private long uncheckedEvents(UUID owner) {
        try { return events(owner); }
        catch (Exception failure) { throw new AssertionError(failure); }
    }

    private int rows(String value) {
        try (var manager = factory.createEntityManager()) {
            return manager.createQuery("select c from UniqueClaim c where c.id like :suffix", Object.class)
                    .setParameter("suffix", "%" + UniqueValues.key(List.of(value), true)).getResultList().size();
        }
    }

    private long eventCount(List<UUID> owners) throws Exception {
        long total = 0;
        for (var owner : owners) total += events(owner);
        return total;
    }

    private static List<UUID> owners() {
        var result = new ArrayList<UUID>();
        for (var index = 0; index < CONTENDERS; index++) result.add(UUID.randomUUID());
        return result;
    }

    private static int winners(List<CompletableFuture<Boolean>> calls) throws Exception {
        var count = 0;
        for (var call : calls) if (call.get(10, TimeUnit.SECONDS)) count++;
        return count;
    }

    protected static void await(CyclicBarrier barrier) {
        try { barrier.await(5, TimeUnit.SECONDS); }
        catch (Exception failure) { throw new AssertionError("All contenders must arrive", failure); }
    }

    protected static Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
