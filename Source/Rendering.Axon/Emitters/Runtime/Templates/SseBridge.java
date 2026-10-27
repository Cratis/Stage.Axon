package __RUNTIME__;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.time.Duration;
import java.util.concurrent.Flow;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

public final class SseBridge {
    private SseBridge() {}

    public static <T> SseEmitter stream(Flow.Publisher<T> publisher) {
        return stream(publisher, Duration.ofMinutes(30));
    }

    public static <T> SseEmitter stream(Flow.Publisher<T> publisher, Duration timeout) {
        var emitter = new SseEmitter(timeout.toMillis());
        commitOnInitialise(emitter);
        var subscriber = new StreamSubscriber<T>(emitter);
        emitter.onCompletion(subscriber::cancel);
        emitter.onTimeout(subscriber::cancel);
        emitter.onError(ignored -> subscriber.cancel());
        publisher.subscribe(subscriber);
        return emitter;
    }

    /**
     * Spring keeps sends made before it initialises the emitter and writes them on the request thread, under the emitter's
     * write lock, while it initialises it. Queuing one comment first makes that initialisation always commit the response,
     * so no other thread writes the headers while the request thread is still leaving the servlet filters. Every later
     * send, heartbeat and completion takes the same lock, so it reaches the response only after that commit.
     */
    private static void commitOnInitialise(SseEmitter emitter) {
        try {
            emitter.send(SseEmitter.event().comment("open"));
        } catch (IOException unreachable) {
            throw new UncheckedIOException(unreachable);
        }
    }

    private static final class StreamSubscriber<T> implements Flow.Subscriber<T> {
        private final SseEmitter emitter;
        private final AtomicReference<Flow.Subscription> subscription = new AtomicReference<>();
        private final AtomicBoolean cancelled = new AtomicBoolean();

        private StreamSubscriber(SseEmitter emitter) { this.emitter = emitter; }

        @Override
        public void onSubscribe(Flow.Subscription value) {
            if (!subscription.compareAndSet(null, value) || cancelled.get()) value.cancel();
            else value.request(1);
        }

        @Override
        public void onNext(T value) {
            if (cancelled.get()) return;
            try {
                emitter.send(value);
                subscription.get().request(1);
            } catch (IOException | IllegalStateException exception) { onError(exception); }
        }

        @Override
        public void onError(Throwable error) { cancel(); emitter.completeWithError(error); }

        @Override
        public void onComplete() { cancel(); emitter.complete(); }

        private void cancel() {
            cancelled.set(true);
            var value = subscription.get();
            if (value != null) value.cancel();
        }
    }
}
