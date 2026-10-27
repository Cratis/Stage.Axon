package __RUNTIME__;

import java.io.IOException;
import java.time.Duration;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executors;
import java.util.concurrent.Flow;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.ApplicationListener;
import org.springframework.context.event.ContextClosedEvent;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

/** Owns every open live SSE stream: heartbeat, lifetime, application-wide cap and shutdown. */
@Component
public final class LiveStreams implements ApplicationListener<ContextClosedEvent> {
    private final int maxStreams;
    private final Duration heartbeat;
    private final Duration timeout;
    private final AtomicInteger total = new AtomicInteger();
    private final AtomicBoolean closing = new AtomicBoolean();
    private final Set<OpenStream> sessions = ConcurrentHashMap.newKeySet();
    private final ScheduledExecutorService scheduler = Executors.newSingleThreadScheduledExecutor(task -> {
        var thread = new Thread(task, "live-stream-heartbeat");
        thread.setDaemon(true);
        return thread;
    });

    public LiveStreams(
            @Value("${stage.axon.live.max-streams:1024}") int maxStreams,
            @Value("${stage.axon.live.heartbeat:15s}") Duration heartbeat,
            @Value("${stage.axon.live.timeout:30m}") Duration timeout) {
        this.maxStreams = maxStreams;
        this.heartbeat = heartbeat;
        this.timeout = timeout;
    }

    public Group group() { return new Group(); }

    public int active() { return total.get(); }

    @Override
    public void onApplicationEvent(ContextClosedEvent event) {
        closing.set(true);
        sessions.forEach(OpenStream::stop);
        scheduler.shutdownNow();
    }

    public final class Group {
        private final AtomicInteger count = new AtomicInteger();

        private Group() {}

        public int activeCount() { return count.get(); }

        public SseEmitter open(Flow.Publisher<Object> publisher) {
            if (maxStreams > 0 && total.incrementAndGet() > maxStreams) {
                total.decrementAndGet();
                throw new LiveCapacityException();
            }
            if (maxStreams <= 0) total.incrementAndGet();
            count.incrementAndGet();
            var session = new OpenStream(this);
            sessions.add(session);
            if (closing.get()) session.stop();
            var emitter = SseBridge.stream(downstream -> publisher.subscribe(session.forwardTo(downstream)), timeout);
            session.attach(emitter);
            return emitter;
        }
    }

    private final class OpenStream {
        private final Group group;
        private final AtomicReference<Flow.Subscription> subscription = new AtomicReference<>();
        private final AtomicBoolean cancelled = new AtomicBoolean();
        private volatile SseEmitter emitter;
        private volatile ScheduledFuture<?> beat;

        private OpenStream(Group group) { this.group = group; }

        private Flow.Subscriber<Object> forwardTo(Flow.Subscriber<? super Object> downstream) {
            return new Flow.Subscriber<>() {
                @Override public void onSubscribe(Flow.Subscription value) {
                    if (!subscription.compareAndSet(null, value)) { value.cancel(); return; }
                    downstream.onSubscribe(new Flow.Subscription() {
                        @Override public void request(long amount) { if (!cancelled.get()) value.request(amount); }
                        @Override public void cancel() { OpenStream.this.cancel(); }
                    });
                    if (cancelled.get()) value.cancel();
                }
                @Override public void onNext(Object value) { if (!cancelled.get()) downstream.onNext(value); }
                @Override public void onError(Throwable failure) { cancel(); downstream.onError(failure); }
                @Override public void onComplete() { cancel(); downstream.onComplete(); }
            };
        }

        private void attach(SseEmitter value) {
            emitter = value;
            value.onCompletion(this::cancel);
            value.onTimeout(this::stop);
            value.onError(failure -> cancel());
            if (cancelled.get() || closing.get()) { value.complete(); return; }
            if (!heartbeat.isZero() && !heartbeat.isNegative()) {
                try {
                    beat = scheduler.scheduleWithFixedDelay(this::beat, heartbeat.toMillis(), heartbeat.toMillis(), TimeUnit.MILLISECONDS);
                } catch (RuntimeException rejected) { stop(); return; }
                if (cancelled.get()) beat.cancel(false);
            }
        }

        private void beat() {
            try {
                emitter.send(SseEmitter.event().comment("heartbeat"));
            } catch (IOException | IllegalStateException failure) { cancel(); }
        }

        private void cancel() {
            if (!cancelled.compareAndSet(false, true)) return;
            sessions.remove(this);
            group.count.decrementAndGet();
            total.decrementAndGet();
            var timer = beat;
            if (timer != null) timer.cancel(false);
            var active = subscription.get();
            if (active != null) active.cancel();
        }

        private void stop() {
            cancel();
            var active = emitter;
            if (active != null) active.complete();
        }
    }
}
