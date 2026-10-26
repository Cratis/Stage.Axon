// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class DefaultViewDispatchProof
{
    internal const string Source = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import __RUNTIME__.ReadModelStore;
        import java.util.List;
        import java.util.Optional;
        import java.util.concurrent.CopyOnWriteArrayList;
        import java.util.concurrent.CountDownLatch;
        import java.util.concurrent.TimeUnit;
        import java.util.concurrent.atomic.AtomicInteger;
        import java.util.concurrent.atomic.AtomicReference;
        import java.util.function.UnaryOperator;
        import org.junit.jupiter.api.Test;
        import org.reactivestreams.Subscriber;
        import org.reactivestreams.Subscription;
        import org.axonframework.eventsourcing.eventstore.EventStore;
        import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
        import org.axonframework.messaging.eventhandling.GenericEventMessage;
        import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
        import org.springframework.beans.factory.config.BeanPostProcessor;
        import org.springframework.boot.SpringApplication;
        import org.springframework.boot.autoconfigure.EnableAutoConfiguration;
        import org.springframework.context.annotation.Bean;
        import org.springframework.boot.test.context.TestConfiguration;
        import org.springframework.context.annotation.Import;
        import static org.junit.jupiter.api.Assertions.*;

        final class __NAME__DefaultDispatchTest {
            @TestConfiguration(proxyBeanMethods = false)
            @EnableAutoConfiguration
            @Import({__APPLICATION__.infrastructure.AxonConfiguration.class, __APPLICATION__.infrastructure.SecurityConfiguration.class,
                    __MODEL__StoreConfiguration.class, __PROJECTOR__.class, __LIVE_QUERY__Handler.class})
            static class DefaultFixture {
                @Bean static BeanPostProcessor observeDefaultStore() {
                    return new BeanPostProcessor() {
                        @Override @SuppressWarnings("unchecked")
                        public Object postProcessAfterInitialization(Object bean, String name) {
                            return name.equals("__QUALIFIER__") ? new ObservedStore((ReadModelStore<Object, __MODEL__>) bean) : bean;
                        }
                    };
                }
            }

            // Observe the real default store without replacing projector/handler discovery or query emission.
            static final class ObservedStore implements ReadModelStore<Object, __MODEL__> {
                final ReadModelStore<Object, __MODEL__> delegate;
                final AtomicInteger writes = new AtomicInteger();
                ObservedStore(ReadModelStore<Object, __MODEL__> delegate) { this.delegate = delegate; }
                @Override public Optional<__MODEL__> get(Object key) { return delegate.get(key); }
                @Override public List<__MODEL__> all() { return delegate.all(); }
                @Override public void put(Object key, __MODEL__ value) { writes.incrementAndGet(); delegate.put(key, value); }
                @Override public void remove(Object key) { writes.incrementAndGet(); delegate.remove(key); }
                @Override public void clear() { writes.incrementAndGet(); delegate.clear(); }
                @Override public __MODEL__ update(Object key, UnaryOperator<__MODEL__> update) { writes.incrementAndGet(); return delegate.update(key, update); }
            }

            @Test void discoversContextualHandlerOnceAndInvalidatesDefaultQueryAfterWrite() throws Exception {
                var spring = new SpringApplication(DefaultFixture.class);
                spring.setDefaultProperties(java.util.Map.of(
                        "server.port", "0", "spring.profiles.active", "default",
                        "axon.eventhandling.processors.__PACKAGE__.mode", "subscribing"));
                try (var application = spring.run()) {
                    assertFalse(java.util.Arrays.asList(application.getEnvironment().getActiveProfiles()).contains("persistent"));
                    var store = (ObservedStore) application.getBean("__QUALIFIER__");
                    var gateway = application.getBean(QueryGateway.class);
                    var signals = new CopyOnWriteArrayList<__LIVE_QUERY__.Result>();
                    var initial = new CountDownLatch(1);
                    var failure = new AtomicReference<Throwable>();
                    var active = new AtomicReference<Subscription>();
                    var writesAtInvalidation = new AtomicInteger(-1);
                    gateway.subscriptionQuery(new __LIVE_QUERY__(), __LIVE_QUERY__.Result.class).subscribe(new Subscriber<__LIVE_QUERY__.Result>() {
                        @Override public void onSubscribe(Subscription subscription) { active.set(subscription); subscription.request(Long.MAX_VALUE); }
                        @Override public void onNext(__LIVE_QUERY__.Result result) {
                            if (result.invalidated()) writesAtInvalidation.set(store.writes.get());
                            signals.add(result);
                            initial.countDown();
                        }
                        @Override public void onError(Throwable cause) { failure.set(cause); initial.countDown(); }
                        @Override public void onComplete() { }
                    });
                    try {
                        assertTrue(initial.await(20, TimeUnit.SECONDS));
                        assertNull(failure.get());
                        assertEquals(1, signals.size());
                        assertFalse(signals.getFirst().invalidated());
                        assertTrue(signals.getFirst().rows().isEmpty());
                        assertEquals(0, store.writes.get());
                        // Only Axon's discovered @EventHandler may dispatch this event and resolve ProcessingContext.
                        var eventStore = application.getBean(EventStore.class);
                        application.getBean(UnitOfWorkFactory.class).create().executeWithResult(context -> {
                            eventStore.appendEvent(context, new GenericEventMessage(__EVENT__));
                            return "appended";
                        }).get(20, TimeUnit.SECONDS);
                        assertNull(failure.get());
                        assertEquals(1, store.writes.get(), "Both on and handle must not be discovered for the same event");
                        assertEquals(1, store.all().size(), "The discovered handler must create one row");
                        assertEquals(2, signals.size(), "Subscribing default processing must invalidate before append completes");
                        assertTrue(signals.getLast().invalidated());
                        assertEquals(1, writesAtInvalidation.get(), "Invalidation must follow the in-memory update");
                        var snapshot = gateway.query(new __LIVE_QUERY__(), __LIVE_QUERY__.Result.class).get(20, TimeUnit.SECONDS);
                        assertEquals(store.all(), snapshot.rows());
                        assertEquals(2, signals.size(), "One event must emit exactly one invalidation");
                    } finally { if (active.get() != null) active.get().cancel(); }
                }
            }
        }
        """;
}
