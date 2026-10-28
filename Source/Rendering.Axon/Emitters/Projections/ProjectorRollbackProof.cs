// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ProjectorRollbackProof
{
    internal const string Source = """
        package __PACKAGE__;

        import __RUNTIME__.InMemoryReadModelStore;
        import __RUNTIME__.ReadModelStore;
        import java.util.List;
        import java.util.Optional;
        import java.util.Set;
        import java.util.concurrent.CompletableFuture;
        import java.util.concurrent.CountDownLatch;
        import java.util.concurrent.Executors;
        import java.util.concurrent.TimeUnit;
        import java.util.concurrent.TimeoutException;
        import java.util.function.Consumer;
        import java.util.function.UnaryOperator;
        import org.axonframework.messaging.core.ApplicationContext;
        import org.axonframework.messaging.core.Message;
        import org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver;
        import org.axonframework.messaging.core.unitofwork.ProcessingContext;
        import org.axonframework.messaging.core.unitofwork.SimpleUnitOfWorkFactory;
        import org.axonframework.messaging.eventhandling.EventMessage;
        import org.axonframework.messaging.eventhandling.GenericEventMessage;
        import org.junit.jupiter.api.Assertions;
        import org.junit.jupiter.api.Test;
        import org.junit.jupiter.api.Timeout;

        @Timeout(60)
        class __NAME__ {
            private final SimpleUnitOfWorkFactory factory = new SimpleUnitOfWorkFactory(new ApplicationContext() {
                @Override public <C> C component(Class<C> type, String name) { throw new UnsupportedOperationException(type.getName()); }
            });

            private static __PROJECTOR__ projector(ReadModelStore<Object, __MODEL__> store) {
                return __CONSTRUCTION__;
            }

        __TESTS__
            private static final class FailingStore<V> implements ReadModelStore<Object, V> {
                private final ReadModelStore<Object, V> inner;
                private volatile boolean armed;

                FailingStore(ReadModelStore<Object, V> inner) {
                    this.inner = inner;
                }

                private void check() {
                    if (armed) throw new IllegalStateException("projection-failure-injected");
                }

                @Override public Optional<V> get(Object key) { check(); return inner.get(key); }
                @Override public List<V> all() { check(); return inner.all(); }
                @Override public void put(Object key, V value) { check(); inner.put(key, value); }
                @Override public void remove(Object key) { check(); inner.remove(key); }
                @Override public void clear() { check(); inner.clear(); }
                @Override public V update(Object key, UnaryOperator<V> update) { check(); return inner.update(key, update); }
            }

            private void rejectedInCommit(ProcessingContext context) {
                context.runOnCommit(committing -> { throw new IllegalStateException("rollback-injected"); });
            }

            private void run(Consumer<ProcessingContext> work) {
                factory.create().executeWithResult(context -> {
                    work.accept(context);
                    return CompletableFuture.completedFuture(null);
                }).join();
            }

            private static ProcessingContext context(ProcessingContext context, Object event) {
                return Message.addToContext(context, message(event));
            }

            private static EventMessage message(Object event) {
                return new GenericEventMessage(new AnnotationMessageTypeResolver().resolve(event.getClass()).orElseThrow(), event);
            }

            private static void await(CountDownLatch latch) {
                try { Assertions.assertTrue(latch.await(20, TimeUnit.SECONDS)); }
                catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); throw new IllegalStateException(interrupted); }
            }

            private static String messages(Throwable failure) {
                var text = new StringBuilder();
                for (Throwable cause = failure; cause != null; cause = cause.getCause()) text.append(cause.getMessage());
                return text.toString();
            }
        }
        """;

    internal const string Specification = """
            @Test void __METHOD__() {
                var expected = new InMemoryReadModelStore<Object, __MODEL__>();
                var oracle = projector(expected);
        __COMMITTED_REFERENCE__
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = projector(store);
                var failure = Assertions.assertThrows(Exception.class, () -> run(context -> {
        __ROLLED__
                    rejectedInCommit(context);
                }));
                Assertions.assertTrue(messages(failure).contains("rollback-injected"), messages(failure));
                Assertions.assertTrue(store.all().isEmpty(), "A rolled-back unit of work must leave no rows");
        __EMPTY_SNAPSHOT__
        __COMMITTED__
                Assertions.assertEquals(Set.copyOf(expected.all()), Set.copyOf(store.all()), "rolled-back events must leave no scope state behind");
        __SNAPSHOT__
            }

            @Test void __METHOD__AfterAFailedProjection() {
                var expected = new InMemoryReadModelStore<Object, __MODEL__>();
                var oracle = projector(expected);
        __COMMITTED_REFERENCE__
                var store = new FailingStore<__MODEL__>(new InMemoryReadModelStore<>());
                var projector = projector(store);
                store.armed = true;
                Assertions.assertThrows(Exception.class, () -> run(context -> {
        __ROLLED__
                    rejectedInCommit(context);
                }));
                store.armed = false;
                Assertions.assertTrue(store.all().isEmpty(), "A failed projection must leave no rows");
        __COMMITTED__
                Assertions.assertEquals(Set.copyOf(expected.all()), Set.copyOf(store.all()), "a projection that failed part-way must leave no scope state behind");
            }

        """;

    internal const string Concurrency = """
            private void concurrent(boolean commits) throws Exception {
                var store = new InMemoryReadModelStore<Object, __MODEL__>();
                var projector = projector(store);
                var staged = new CountDownLatch(1);
                var release = new CountDownLatch(1);
                var competing = new CountDownLatch(1);
                try (var executor = Executors.newVirtualThreadPerTaskExecutor()) {
                    var first = executor.submit(() -> {
                        try {
                            run(context -> {
        __ALL_FIRST__
                                staged.countDown();
                                await(release);
                                if (!commits) rejectedInCommit(context);
                            });
                        } catch (Exception failure) {
                            Assertions.assertFalse(commits, messages(failure));
                            Assertions.assertTrue(messages(failure).contains("rollback-injected"), messages(failure));
                        }
                    });
                    await(staged);
                    var second = executor.submit(() -> run(context -> {
                        competing.countDown();
        __ALL_SECOND__
                    }));
                    try {
                        await(competing);
                        Assertions.assertThrows(TimeoutException.class, () -> second.get(100, TimeUnit.MILLISECONDS));
                        Assertions.assertTrue(store.all().isEmpty(), "Uncommitted projector state must remain invisible");
                    } finally { release.countDown(); }
                    first.get(20, TimeUnit.SECONDS);
                    second.get(20, TimeUnit.SECONDS);
                }
                var expected = new InMemoryReadModelStore<Object, __MODEL__>();
                var oracle = projector(expected);
                for (var round = 0; round < (commits ? 2 : 1); round++) {
        __ORACLE__
                }
                Assertions.assertEquals(Set.copyOf(expected.all()), Set.copyOf(store.all()), "a competing unit of work must observe exactly the committed state");
            }

            @Test void aRolledBackUnitOfWorkDoesNotLeakIntoACompetingOne() throws Exception {
                concurrent(false);
            }

            @Test void aCommittedUnitOfWorkIsNotLostToACompetingOne() throws Exception {
                concurrent(true);
            }

        """;
}
