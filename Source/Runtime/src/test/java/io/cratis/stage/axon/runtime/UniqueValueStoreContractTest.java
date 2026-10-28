package io.cratis.stage.axon.runtime;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.function.Consumer;
import org.axonframework.messaging.core.ApplicationContext;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.SimpleUnitOfWorkFactory;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

@Timeout(30)
abstract class UniqueValueStoreContractTest {
    protected abstract UniqueValueStore store();

    protected SimpleUnitOfWorkFactory factory() {
        return new SimpleUnitOfWorkFactory(new ApplicationContext() {
            @Override public <C> C component(Class<C> type, String name) { throw new UnsupportedOperationException(type.getName()); }
        });
    }

    @Test
    void rejectsASecondOccurrenceButAllowsOtherOwnersAndConstraints() {
        var constraint = unique();
        run(context -> store().occurOnce(context, constraint, "owner", "once"));
        conflict(() -> run(context -> store().occurOnce(context, constraint, "owner", "once")));
        run(context -> store().occurOnce(context, constraint, "other", "once"));
        run(context -> store().occurOnce(context, unique(), "owner", "once"));
    }

    @Test
    void rejectsASecondOccurrenceInTheSameUnitOfWorkAndRollsBack() {
        var constraint = unique();
        conflict(() -> run(context -> {
            store().occurOnce(context, constraint, "owner", "once");
            store().occurOnce(context, constraint, "owner", "once");
        }));
        run(context -> store().occurOnce(context, constraint, "owner", "once"));
    }

    @Test
    void rollbackFreesTheOccurrence() {
        var constraint = unique();
        assertThrows(Exception.class, () -> run(context -> {
            store().occurOnce(context, constraint, "owner", "once");
            throw new IllegalStateException("rollback");
        }));
        run(context -> store().occurOnce(context, constraint, "owner", "once"));
    }

    @Test
    void releaseFreesTheOccurrenceIncludingInTheSameUnitOfWork() {
        var constraint = unique();
        run(context -> store().occurOnce(context, constraint, "owner", "once"));
        run(context -> {
            store().releaseOwner(context, constraint, "owner");
            store().occurOnce(context, constraint, "owner", "once");
        });
        run(context -> store().releaseOwner(context, constraint, "owner"));
        run(context -> store().occurOnce(context, constraint, "owner", "once"));
    }

    @Test
    void rollingBackAReleaseKeepsTheOccurrence() {
        var constraint = unique();
        run(context -> store().occurOnce(context, constraint, "owner", "once"));
        assertThrows(Exception.class, () -> run(context -> {
            store().releaseOwner(context, constraint, "owner");
            throw new IllegalStateException("rollback");
        }));
        conflict(() -> run(context -> store().occurOnce(context, constraint, "owner", "once")));
    }

    @ParameterizedTest
    @ValueSource(booleans = {false, true})
    void aContenderCannotPassAnUncommittedReservation(boolean property) throws Exception {
        var constraint = unique();
        var reserved = new CountDownLatch(1);
        var release = new CountDownLatch(1);
        var contending = new CountDownLatch(1);
        try (var workers = Executors.newFixedThreadPool(2)) {
            var holder = workers.submit(() -> run(context -> {
                reserve(context, constraint, property, "owner");
                reserved.countDown();
                await(release);
            }));
            try {
                assertTrue(reserved.await(5, TimeUnit.SECONDS));
                var contender = workers.submit(() -> conflict(() -> run(context -> {
                    contending.countDown();
                    reserve(context, constraint, property, property ? "other" : "owner");
                })));
                assertTrue(contending.await(5, TimeUnit.SECONDS));
                awaitContender(contender);
                release.countDown();
                holder.get(10, TimeUnit.SECONDS);
            } finally { release.countDown(); }
        }
    }

    @Test
    void exactlyOneConcurrentOccurrenceWins() throws Exception {
        var constraint = unique();
        var ready = new CountDownLatch(8);
        var start = new CountDownLatch(1);
        try (var workers = Executors.newFixedThreadPool(8)) {
            var calls = new ArrayList<CompletableFuture<Boolean>>();
            for (var index = 0; index < 8; index++) {
                calls.add(CompletableFuture.supplyAsync(() -> {
                    ready.countDown();
                    await(start);
                    try {
                        run(context -> store().occurOnce(context, constraint, "owner", "once"));
                        return true;
                    } catch (Exception failure) {
                        assertInstanceOf(UniqueValueConflict.class, root(failure));
                        return false;
                    }
                }, workers));
            }
            assertTrue(ready.await(5, TimeUnit.SECONDS));
            start.countDown();
            var winners = 0;
            for (var call : calls) if (call.get(10, TimeUnit.SECONDS)) winners++;
            assertEquals(1, winners);
        } finally { start.countDown(); }
    }

    protected void awaitContender(Future<?> contender) throws Exception {
        contender.get(3, TimeUnit.SECONDS);
    }

    private void reserve(ProcessingContext context, String constraint, boolean property, String owner) {
        if (property) store().assign(context, constraint, List.of("value"), owner, false, "once");
        else store().occurOnce(context, constraint, owner, "once");
    }

    protected void run(Consumer<ProcessingContext> action) {
        factory().create().executeWithResult(context -> {
            action.accept(context);
            return CompletableFuture.completedFuture(null);
        }).join();
    }

    private static void conflict(Runnable action) {
        var failure = assertThrows(Exception.class, action::run);
        assertInstanceOf(UniqueValueConflict.class, root(failure));
        assertEquals("once", root(failure).getMessage());
    }

    private static Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }

    private static void await(CountDownLatch latch) {
        try { assertTrue(latch.await(5, TimeUnit.SECONDS)); }
        catch (InterruptedException failure) {
            Thread.currentThread().interrupt();
            throw new AssertionError(failure);
        }
    }

    private static String unique() { return UUID.randomUUID().toString(); }
}
