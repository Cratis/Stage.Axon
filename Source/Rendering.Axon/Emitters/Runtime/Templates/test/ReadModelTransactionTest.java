package __RUNTIME__;

import java.time.Duration;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Consumer;
import java.util.function.UnaryOperator;
import org.axonframework.messaging.core.ApplicationContext;
import org.axonframework.messaging.core.unitofwork.ProcessingContext;
import org.axonframework.messaging.core.unitofwork.SimpleUnitOfWorkFactory;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.Timeout;

@Timeout(30)
class ReadModelTransactionTest {
    private final SimpleUnitOfWorkFactory factory = new SimpleUnitOfWorkFactory(new ApplicationContext() {
        @Override public <C> C component(Class<C> type, String name) { throw new UnsupportedOperationException(type.getName()); }
    });

    @Test void discardsFailedWritesAndRetainsCommittedRows() {
        var store = new InMemoryReadModelStore<String, Long>();
        store.put("existing", 4L);
        Assertions.assertThrows(Exception.class, () -> run(context -> {
            var bound = ReadModelTransaction.bind(store, context);
            Assertions.assertSame(bound, ReadModelTransaction.bind(store, context));
            bound.update("existing", value -> value + 1);
            bound.put("new", 8L);
            Assertions.assertEquals(5L, bound.get("existing").orElseThrow());
            Assertions.assertEquals(4L, store.get("existing").orElseThrow());
            Assertions.assertTrue(store.get("new").isEmpty());
            throw new IllegalStateException("handler rejected");
        }));
        Assertions.assertEquals(4L, store.get("existing").orElseThrow());
        Assertions.assertTrue(store.get("new").isEmpty());
        run(context -> ReadModelTransaction.bind(store, context).update("existing", value -> value + 1));
        Assertions.assertEquals(5L, store.get("existing").orElseThrow());
    }

    @Test void discardsRemovesOnRollbackAndCommitsThemOnSuccess() {
        var store = new InMemoryReadModelStore<String, Long>();
        store.put("row", 1L);
        Assertions.assertThrows(Exception.class, () -> run(context -> {
            var bound = ReadModelTransaction.bind(store, context);
            bound.remove("row");
            Assertions.assertTrue(bound.get("row").isEmpty());
            throw new IllegalStateException("rollback remove");
        }));
        Assertions.assertEquals(1L, store.get("row").orElseThrow());
        run(context -> ReadModelTransaction.bind(store, context).remove("row"));
        Assertions.assertTrue(store.get("row").isEmpty());
    }

    @Test void publishesCommittedWrapperChangesBeforeAfterCommitCallbacks() {
        var delegate = new InMemoryReadModelStore<String, Long>();
        var changes = new AtomicInteger();
        ReadModelStore<String, Long> wrapper = new ReadModelStore<>() {
            public Optional<Long> get(String key) { return delegate.get(key); }
            public List<Long> all() { return delegate.all(); }
            public void put(String key, Long value) { delegate.put(key, value); changes.incrementAndGet(); }
            public void remove(String key) { delegate.remove(key); changes.incrementAndGet(); }
            public void clear() { delegate.clear(); }
            public Long update(String key, UnaryOperator<Long> update) { return delegate.update(key, update); }
        };
        var notified = new AtomicBoolean();
        run(context -> {
            ReadModelTransaction.bind(wrapper, context).put("row", 7L);
            Assertions.assertEquals(0, changes.get());
            context.runOnAfterCommit(completed -> {
                Assertions.assertEquals(7L, wrapper.get("row").orElseThrow());
                Assertions.assertEquals(1, changes.get());
                notified.set(true);
            });
        });
        Assertions.assertTrue(notified.get());
    }

    @Test void doesNotLetOneRollbackEraseAnotherCommittedUnitOfWork() throws Exception {
        var store = new InMemoryReadModelStore<String, Long>();
        var staged = new CountDownLatch(1);
        var release = new CountDownLatch(1);
        var competing = new CountDownLatch(1);
        try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
            var rejected = executor.submit(() -> Assertions.assertThrows(Exception.class, () -> run(context -> {
                ReadModelTransaction.bind(store, context).put("row", 100L);
                staged.countDown();
                await(release);
                throw new IllegalStateException("rollback owner");
            })));
            await(staged);
            var committed = executor.submit(() -> run(context -> {
                competing.countDown();
                ReadModelTransaction.bind(store, context).update("row", value -> value == null ? 1L : value + 1);
            }));
            try {
                await(competing);
                Assertions.assertThrows(TimeoutException.class, () -> committed.get(100, TimeUnit.MILLISECONDS));
                Assertions.assertTrue(store.get("row").isEmpty());
            } finally { release.countDown(); }
            rejected.get(20, TimeUnit.SECONDS);
            committed.get(20, TimeUnit.SECONDS);
        }
        Assertions.assertEquals(1L, store.get("row").orElseThrow());
    }

    @Test void failsTheUnitOfWorkVisiblyWhenReservationTimesOut() throws Exception {
        var store = new InMemoryReadModelStore<String, Long>();
        var staged = new CountDownLatch(1);
        var release = new CountDownLatch(1);
        var proceeded = new AtomicBoolean();
        var committed = new AtomicBoolean();
        try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
            var owner = executor.submit(() -> run(context -> {
                ReadModelTransaction.bind(store, context).put("row", 1L);
                staged.countDown();
                await(release);
            }));
            try {
                await(staged);
                var failure = Assertions.assertThrows(Exception.class, () -> run(context -> {
                    context.runOnAfterCommit(done -> committed.set(true));
                    var bound = ReadModelTransaction.bind(store, context);
                    bound.put("other", 9L);
                    bound.put("row", 2L);
                    proceeded.set(true);
                }));
                Assertions.assertTrue(cause(failure).getMessage().contains("another unit of work"));
                Assertions.assertFalse(proceeded.get());
                Assertions.assertFalse(committed.get());
                Assertions.assertTrue(store.all().isEmpty());
            } finally { release.countDown(); }
            owner.get(20, TimeUnit.SECONDS);
        }
        Assertions.assertEquals(1L, store.get("row").orElseThrow());
        run(context -> ReadModelTransaction.bind(store, context).put("other", 3L));
        Assertions.assertEquals(3L, store.get("other").orElseThrow());
    }

    @Test void serializesTwoKeyUpdatesAcquiredInSortedOrderWithoutDeadlock() throws Exception {
        var store = new InMemoryReadModelStore<String, Long>();
        var staged = new CountDownLatch(1);
        var release = new CountDownLatch(1);
        var competing = new CountDownLatch(1);
        try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
            var first = executor.submit(() -> run(context -> {
                var bound = ReadModelTransaction.bind(store, context);
                for (var key : List.of("b", "a").stream().sorted().toList()) bound.update(key, value -> 1L);
                staged.countDown();
                await(release);
            }));
            await(staged);
            var second = executor.submit(() -> run(context -> {
                var bound = ReadModelTransaction.bind(store, context);
                competing.countDown();
                for (var key : List.of("a", "b").stream().sorted().toList()) bound.update(key, value -> value + 1);
            }));
            try {
                await(competing);
                Assertions.assertThrows(TimeoutException.class, () -> second.get(100, TimeUnit.MILLISECONDS));
            } finally { release.countDown(); }
            first.get(20, TimeUnit.SECONDS);
            second.get(20, TimeUnit.SECONDS);
        }
        Assertions.assertEquals(List.of(2L, 2L), store.all());
    }

    @Test void doesNotDeadlockWhenTwoUnitsOfWorkTouchTheSameKeysInOppositeOrder() {
        Assertions.assertTimeoutPreemptively(Duration.ofSeconds(5), () -> {
            var store = new InMemoryReadModelStore<String, Long>();
            var entered = new CountDownLatch(2);
            try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
                var deliveries = List.of(List.of("a", "b"), List.of("b", "a")).stream().map(keys -> executor.submit(() -> run(context -> {
                    var bound = ReadModelTransaction.bind(store, context);
                    entered.countDown();
                    await(entered);
                    ReadModelTransaction.reserve(bound, keys);
                    for (var key : keys) bound.update(key, value -> value == null ? 1L : value + 1);
                }))).toList();
                for (var delivery : deliveries) delivery.get(4, TimeUnit.SECONDS);
            }
            Assertions.assertEquals(2L, store.get("a").orElseThrow());
            Assertions.assertEquals(2L, store.get("b").orElseThrow());
        });
    }

    @Test void failsFastOnAReservationCycleAcrossStores() {
        Assertions.assertTimeoutPreemptively(Duration.ofSeconds(2), () -> {
            var first = new InMemoryReadModelStore<String, Long>();
            var second = new InMemoryReadModelStore<String, Long>();
            var held = new CountDownLatch(2);
            var committed = new AtomicInteger();
            var rejected = new AtomicInteger();
            try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
                var deliveries = List.of(List.of(first, second), List.of(second, first)).stream().map(stores -> executor.submit(() -> {
                    try {
                        run(context -> {
                            var own = ReadModelTransaction.bind(stores.get(0), context);
                            var other = ReadModelTransaction.bind(stores.get(1), context);
                            own.put("row", 1L);
                            held.countDown();
                            await(held);
                            other.put("row", 2L);
                        });
                        committed.incrementAndGet();
                    } catch (Exception failure) {
                        Assertions.assertInstanceOf(IllegalStateException.class, cause(failure));
                        Assertions.assertEquals("Read model keys are locked in a cycle with another unit of work", cause(failure).getMessage());
                        rejected.incrementAndGet();
                    }
                })).toList();
                for (var delivery : deliveries) delivery.get(1, TimeUnit.SECONDS);
            }
            Assertions.assertEquals(1, committed.get());
            Assertions.assertEquals(1, rejected.get());
            Assertions.assertEquals(3L, first.get("row").orElseThrow() + second.get("row").orElseThrow());
        });
    }

    @Test void releasesReservationsBeforeAfterCommitCallbacks() {
        Assertions.assertTimeoutPreemptively(Duration.ofSeconds(2), () -> {
            var store = new InMemoryReadModelStore<String, Long>();
            var nestedCommitted = new AtomicBoolean();
            run(context -> {
                ReadModelTransaction.bind(store, context).put("row", 1L);
                context.runOnAfterCommit(completed -> {
                    run(nested -> ReadModelTransaction.bind(store, nested).update("row", value -> value + 1));
                    nestedCommitted.set(true);
                });
            });
            Assertions.assertTrue(nestedCommitted.get());
            Assertions.assertEquals(2L, store.get("row").orElseThrow());
        });
    }

    @Test void refusesMissingContextAndTransactionalScans() {
        var store = new InMemoryReadModelStore<String, Long>();
        Assertions.assertThrows(NullPointerException.class, () -> ReadModelTransaction.bind(store, null));
        run(context -> {
            Assertions.assertThrows(UnsupportedOperationException.class, () -> ReadModelTransaction.bind(store, context).all());
            Assertions.assertThrows(UnsupportedOperationException.class, () -> ReadModelTransaction.bind(store, context).clear());
        });
    }

    private void run(Consumer<ProcessingContext> work) {
        factory.create().executeWithResult(context -> {
            work.accept(context);
            return CompletableFuture.completedFuture(null);
        }).join();
    }
    private static void await(CountDownLatch latch) {
        try { Assertions.assertTrue(latch.await(20, TimeUnit.SECONDS)); }
        catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); throw new IllegalStateException(interrupted); }
    }
    private static Throwable cause(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
