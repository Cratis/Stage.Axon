// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewSubscriptionProof
{
    internal const string Source = """
        package __PACKAGE__;

        import java.util.List;
        import java.util.Set;
        import java.util.concurrent.CopyOnWriteArrayList;
        import java.util.concurrent.CountDownLatch;
        import java.util.concurrent.TimeUnit;
        import java.util.concurrent.atomic.AtomicReference;
        import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
        import org.junit.jupiter.api.Test;
        import org.reactivestreams.Subscriber;
        import org.reactivestreams.Subscription;
        import org.springframework.boot.test.context.TestConfiguration;
        import org.springframework.context.annotation.Import;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;

        /** Real persistent-profile subscription: committed JPA visibility and rollback silence. */
        class __NAME__SubscriptionTest {
            @TestConfiguration(proxyBeanMethods = false)
            @Import(__LIVE_QUERY__Handler.class)
            static class SubscriptionFixture {}

            @Test
            void committedRowsNotifyAndRollbackStaysSilent() throws Exception {
                var path = database();
                failure = Failure.NONE;
                var active = new AtomicReference<Subscription>();
                try (var application = start(path, SubscriptionFixture.class)) {
                    var gateway = application.getBean(QueryGateway.class);
                    var invalidations = new CopyOnWriteArrayList<Boolean>();
                    var observed = new CopyOnWriteArrayList<Set<__MODEL__>>();
                    var error = new AtomicReference<Throwable>();
                    gateway.subscriptionQuery(new __LIVE_QUERY__(), __LIVE_QUERY__.Result.class).subscribe(new Subscriber<__LIVE_QUERY__.Result>() {
                        @Override public void onSubscribe(Subscription subscription) { active.set(subscription); subscription.request(Long.MAX_VALUE); }
                        @Override public void onNext(__LIVE_QUERY__.Result result) {
                            invalidations.add(result.invalidated());
                            observed.add(result.invalidated() ? normalized(store(application).all()) : normalized(result.rows()));
                        }
                        @Override public void onError(Throwable cause) { error.set(cause); }
                        @Override public void onComplete() { }
                    });
                    await(() -> observed.size() == 1, () -> ": initial subscription result, error: " + error.get());
                    assertNull(error.get());
                    assertFalse(invalidations.getFirst());
                    assertTrue(observed.getFirst().isEmpty());

                    append(application, __EVENT__);
                    await(() -> observed.size() == 2, () -> ": committed invalidation, observed: " + observed + ", error: " + error.get());
                    assertEquals(normalized(expected()), observed.get(1), "The invalidation must observe the committed JPA row");
                    assertEquals(normalized(expected()), normalized(gateway.query(new __LIVE_QUERY__(), __LIVE_QUERY__.Result.class).get(20, TimeUnit.SECONDS).rows()));
                    var checkpoint = tokens(application);
                    var counted = count(application);

                    rolledBack = new CountDownLatch(1);
                    tokenRejected = new CountDownLatch(1);
                    installTokenFailure(path);
                    failure = Failure.TOKEN;
                    append(application, __EVENT__);
                    assertTrue(rolledBack.await(20, TimeUnit.SECONDS), "Actual processor must roll back");
                    assertTrue(tokenRejected.await(20, TimeUnit.SECONDS), "Real JpaTokenStore update must reach the rejecting trigger");
                    assertEquals(normalized(expected()), normalized(store(application).all()), "Rollback must preserve the committed JPA row");
                    assertEquals(counted, count(application), "Rollback must preserve the counter");
                    assertEquals(checkpoint, tokens(application), "Rollback must preserve the checkpoint");
                    assertEquals(2, observed.size(), "Rollback must not notify the subscription");

                    failure = Failure.NONE;
                    await(() -> observed.size() >= 3 && normalized(store(application).all()).equals(normalized(next(1))),
                            () -> ": reclaimed invalidation, observed: " + observed + ", rows: " + normalized(store(application).all()) + ", error: " + error.get());
                    assertEquals(List.of(false, true, true), invalidations, "One invalidation per committed unit of work, none for the rollback");
                    assertEquals(normalized(next(1)), observed.get(2), "The retried invalidation must observe the committed JPA row");
                    assertEquals(normalized(next(1)), normalized(gateway.query(new __LIVE_QUERY__(), __LIVE_QUERY__.Result.class).get(20, TimeUnit.SECONDS).rows()));
                    assertEquals(3, observed.size(), "Rollback must not notify the subscription after reclaim");
                    assertNull(error.get());
                } finally {
                    failure = Failure.NONE;
                    if (active.get() != null) active.get().cancel();
                }
            }
        }
        """;
}
