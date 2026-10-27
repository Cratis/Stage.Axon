package __RUNTIME__;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Flow;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.Test;
import org.reactivestreams.Publisher;
import org.reactivestreams.Subscriber;
import org.reactivestreams.Subscription;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class LiveSnapshotsTest {
    record Snap(String value, boolean invalidated) {}

    static final class Upstream implements Publisher<Snap> {
        Subscriber<? super Snap> subscriber;
        volatile boolean cancelled;
        long requested;

        @Override
        public void subscribe(Subscriber<? super Snap> target) {
            subscriber = target;
            target.onSubscribe(new Subscription() {
                @Override public void request(long amount) { requested += amount; }
                @Override public void cancel() { cancelled = true; }
            });
        }

        void initial(String value) { subscriber.onNext(new Snap(value, false)); }
        void invalidate() { subscriber.onNext(new Snap("", true)); }
    }

    static class Collector implements Flow.Subscriber<Snap> {
        final List<String> items = new CopyOnWriteArrayList<>();
        volatile Throwable error;
        volatile boolean completed;
        Flow.Subscription subscription;
        private final long demand;

        Collector(long demand) { this.demand = demand; }

        @Override public void onSubscribe(Flow.Subscription value) { subscription = value; if (demand > 0) value.request(demand); }
        @Override public void onNext(Snap value) { items.add(value.value()); }
        @Override public void onError(Throwable failure) { error = failure; }
        @Override public void onComplete() { completed = true; }
    }

    static final class Reads {
        final List<CompletableFuture<Snap>> futures = new ArrayList<>();

        CompletableFuture<Snap> next() {
            var future = new CompletableFuture<Snap>();
            futures.add(future);
            return future;
        }

        void complete(int index, String value) { futures.get(index).complete(new Snap(value, false)); }
    }

    private static Flow.Publisher<Snap> publisher(Upstream upstream, Reads reads) {
        return LiveSnapshots.of(() -> upstream, Snap::invalidated, reads::next, Runnable::run);
    }

    @Test
    void theInitialSnapshotIsDeliveredOnDemandOnly() {
        var upstream = new Upstream();
        var collector = new Collector(0);
        publisher(upstream, new Reads()).subscribe(collector);
        upstream.initial("a");
        assertTrue(collector.items.isEmpty());
        collector.subscription.request(1);
        assertEquals(List.of("a"), collector.items);
    }

    @Test
    void invalidationsWithoutDemandCostNoReadsAndCoalesceIntoOne() {
        var upstream = new Upstream();
        var reads = new Reads();
        var collector = new Collector(1);
        publisher(upstream, reads).subscribe(collector);
        upstream.initial("a");
        for (var index = 0; index < 5; index++) upstream.invalidate();
        assertEquals(0, reads.futures.size());
        collector.subscription.request(1);
        assertEquals(1, reads.futures.size());
        reads.complete(0, "b");
        assertEquals(List.of("a", "b"), collector.items);
    }

    @Test
    void anInvalidationDuringTheInitialHoldReplacesTheStaleInitialSnapshot() {
        var upstream = new Upstream();
        var reads = new Reads();
        var collector = new Collector(0);
        publisher(upstream, reads).subscribe(collector);
        upstream.initial("old");
        upstream.invalidate();
        collector.subscription.request(1);
        reads.complete(0, "new");
        assertEquals(List.of("new"), collector.items);
    }

    @Test
    void anEqualSnapshotIsSuppressedAndKeepsItsDemand() {
        var upstream = new Upstream();
        var reads = new Reads();
        var collector = new Collector(2);
        publisher(upstream, reads).subscribe(collector);
        upstream.initial("a");
        upstream.invalidate();
        reads.complete(0, "a");
        assertEquals(List.of("a"), collector.items);
        upstream.invalidate();
        reads.complete(1, "b");
        assertEquals(List.of("a", "b"), collector.items);
    }

    @Test
    void atMostOneReadIsInFlightAndANewerMarkCausesOneFollowUp() {
        var upstream = new Upstream();
        var reads = new Reads();
        var collector = new Collector(10);
        publisher(upstream, reads).subscribe(collector);
        upstream.initial("a");
        upstream.invalidate();
        upstream.invalidate();
        upstream.invalidate();
        assertEquals(1, reads.futures.size());
        reads.complete(0, "b");
        assertEquals(2, reads.futures.size());
        reads.complete(1, "c");
        assertEquals(List.of("a", "b", "c"), collector.items);
        assertEquals(2, reads.futures.size());
    }

    @Test
    void cancellingCancelsTheUpstreamAndThePendingReadAndDropsLateResults() {
        var upstream = new Upstream();
        var reads = new Reads();
        var collector = new Collector(10);
        publisher(upstream, reads).subscribe(collector);
        upstream.initial("a");
        upstream.invalidate();
        collector.subscription.cancel();
        assertTrue(upstream.cancelled);
        assertTrue(reads.futures.get(0).isCancelled());
        reads.complete(0, "late");
        assertEquals(List.of("a"), collector.items);
    }

    @Test
    void aFailedReadErrorsAndCancelsTheUpstream() {
        var upstream = new Upstream();
        var reads = new Reads();
        var collector = new Collector(10);
        publisher(upstream, reads).subscribe(collector);
        upstream.initial("a");
        upstream.invalidate();
        reads.futures.get(0).completeExceptionally(new IllegalStateException("boom"));
        assertInstanceOf(IllegalStateException.class, collector.error);
        assertTrue(upstream.cancelled);
    }

    @Test
    void aNonPositiveRequestIsAnError() {
        var upstream = new Upstream();
        var collector = new Collector(0);
        publisher(upstream, new Reads()).subscribe(collector);
        collector.subscription.request(0);
        assertInstanceOf(IllegalArgumentException.class, collector.error);
        assertTrue(upstream.cancelled);
    }

    @Test
    void completionIsDeliveredOnceNothingIsPending() {
        var upstream = new Upstream();
        var collector = new Collector(10);
        publisher(upstream, new Reads()).subscribe(collector);
        upstream.initial("a");
        upstream.subscriber.onComplete();
        assertEquals(List.of("a"), collector.items);
        assertTrue(collector.completed);
        assertNull(collector.error);
    }

    @Test
    void aConsumerBlockedInOnNextDoesNotBlockTheUpstreamCaller() throws Exception {
        var upstream = new Upstream();
        var reads = new Reads();
        var entered = new CountDownLatch(1);
        var release = new CountDownLatch(1);
        var collector = new Collector(10) {
            @Override
            public void onNext(Snap value) {
                entered.countDown();
                try { release.await(10, TimeUnit.SECONDS); } catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); }
                super.onNext(value);
            }
        };
        LiveSnapshots.of(() -> upstream, Snap::invalidated, reads::next).subscribe(collector);
        try {
            upstream.initial("a");
            assertTrue(entered.await(10, TimeUnit.SECONDS));
            var started = System.nanoTime();
            for (var index = 0; index < 100; index++) upstream.invalidate();
            assertTrue(System.nanoTime() - started < TimeUnit.SECONDS.toNanos(2));
        } finally {
            release.countDown();
        }
    }
}
