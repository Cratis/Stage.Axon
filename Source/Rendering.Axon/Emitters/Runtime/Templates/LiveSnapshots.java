package __RUNTIME__;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import java.util.concurrent.Flow;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Predicate;
import java.util.function.Supplier;
import org.reactivestreams.Publisher;
import org.reactivestreams.Subscriber;
import org.reactivestreams.Subscription;

/**
 * Latest-state snapshot delivery for live queries.
 *
 * <p>The Axon subscription is drained eagerly into one dirty flag. A snapshot is only read when the consumer has demand,
 * so a slow consumer costs O(1) memory and no reads. Equal consecutive snapshots are suppressed. Downstream signals are
 * serial and run on an executor, never on the thread that emitted the update and never under a lock.</p>
 */
public final class LiveSnapshots {
    private static final Executor DEFAULT = Executors.newVirtualThreadPerTaskExecutor();

    private LiveSnapshots() {}

    public static <R> Flow.Publisher<R> of(Supplier<Publisher<R>> source, Predicate<R> invalidation, Supplier<CompletableFuture<R>> requery) {
        return of(source, invalidation, requery, DEFAULT);
    }

    static <R> Flow.Publisher<R> of(Supplier<Publisher<R>> source, Predicate<R> invalidation, Supplier<CompletableFuture<R>> requery, Executor executor) {
        return downstream -> source.get().subscribe(new Delivery<>(downstream, invalidation, requery, executor));
    }

    private static final class Delivery<R> implements Subscriber<R>, Flow.Subscription {
        private final Flow.Subscriber<? super R> downstream;
        private final Predicate<R> invalidation;
        private final Supplier<CompletableFuture<R>> requery;
        private final Executor executor;
        private final AtomicInteger wip = new AtomicInteger();
        private final AtomicLong demand = new AtomicLong();
        private final AtomicBoolean dirty = new AtomicBoolean();
        private final AtomicBoolean fetching = new AtomicBoolean();
        private final AtomicReference<R> initial = new AtomicReference<>();
        private final AtomicReference<R> fetched = new AtomicReference<>();
        private final AtomicReference<Subscription> upstream = new AtomicReference<>();
        private final AtomicReference<CompletableFuture<R>> pending = new AtomicReference<>();
        private volatile boolean cancelled;
        private volatile boolean upstreamDone;
        private volatile Throwable failure;
        private boolean terminated;
        private boolean hasLast;
        private R last;

        private Delivery(Flow.Subscriber<? super R> downstream, Predicate<R> invalidation, Supplier<CompletableFuture<R>> requery, Executor executor) {
            this.downstream = downstream;
            this.invalidation = invalidation;
            this.requery = requery;
            this.executor = executor;
        }

        @Override
        public void onSubscribe(Subscription value) {
            if (!upstream.compareAndSet(null, value) || cancelled) { value.cancel(); return; }
            downstream.onSubscribe(this);
            value.request(Long.MAX_VALUE);
        }

        @Override
        public void onNext(R value) {
            if (cancelled) return;
            if (invalidation.test(value)) dirty.set(true);
            else initial.set(value);
            drain();
        }

        @Override
        public void onError(Throwable error) {
            if (failure == null) failure = error;
            drain();
        }

        @Override
        public void onComplete() {
            upstreamDone = true;
            drain();
        }

        @Override
        public void request(long count) {
            if (cancelled) return;
            if (count <= 0) {
                if (failure == null) failure = new IllegalArgumentException("Subscription demand must be positive");
            } else {
                demand.accumulateAndGet(count, (current, added) -> added > Long.MAX_VALUE - current ? Long.MAX_VALUE : current + added);
            }
            drain();
        }

        @Override
        public void cancel() {
            if (cancelled) return;
            cancelled = true;
            release();
            drain();
        }

        private void release() {
            initial.set(null);
            fetched.set(null);
            var value = upstream.get();
            if (value != null) value.cancel();
            var read = pending.getAndSet(null);
            if (read != null) read.cancel(false);
        }

        private void drain() {
            if (wip.getAndIncrement() != 0) return;
            try {
                executor.execute(this::run);
            } catch (RejectedExecutionException rejected) {
                wip.set(0);
                if (failure == null) failure = rejected;
                cancelled = true;
                release();
            }
        }

        private void run() {
            var missed = 1;
            do {
                step();
                missed = wip.addAndGet(-missed);
            } while (missed != 0);
        }

        private void step() {
            while (!terminated) {
                if (cancelled) { terminated = true; return; }
                var error = failure;
                if (error != null) { terminate(error); return; }
                if (demand.get() > 0) {
                    var snapshot = fetched.getAndSet(null);
                    if (snapshot == null) snapshot = takeInitial();
                    if (snapshot != null) { deliver(snapshot); continue; }
                    if (dirty.get() && fetching.compareAndSet(false, true)) { dirty.set(false); fetch(); continue; }
                }
                if (upstreamDone && initial.get() == null && fetched.get() == null && !fetching.get() && !dirty.get()) terminate(null);
                return;
            }
        }

        private R takeInitial() {
            var value = initial.get();
            if (value == null) return null;
            if (dirty.get()) { initial.compareAndSet(value, null); return null; }
            initial.compareAndSet(value, null);
            return value;
        }

        private void deliver(R snapshot) {
            if (hasLast && snapshot.equals(last)) return;
            hasLast = true;
            last = snapshot;
            if (demand.get() != Long.MAX_VALUE) demand.decrementAndGet();
            downstream.onNext(snapshot);
        }

        private void fetch() {
            CompletableFuture<R> read;
            try {
                read = requery.get();
            } catch (RuntimeException | Error thrown) {
                failure = thrown;
                fetching.set(false);
                return;
            }
            pending.set(read);
            if (cancelled) read.cancel(false);
            read.whenComplete((snapshot, error) -> {
                if (cancelled) return;
                if (error != null) failure = error;
                else if (snapshot != null) fetched.set(snapshot);
                fetching.set(false);
                drain();
            });
        }

        private void terminate(Throwable error) {
            terminated = true;
            cancelled = true;
            release();
            if (error != null) downstream.onError(error);
            else downstream.onComplete();
        }
    }
}
