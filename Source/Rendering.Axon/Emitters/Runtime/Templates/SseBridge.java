// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.io.IOException;
import java.util.concurrent.Flow;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

public final class SseBridge {
    private SseBridge() {}

    public static <T> SseEmitter stream(Flow.Publisher<T> publisher) {
        var emitter = new SseEmitter(1_800_000L);
        var subscriber = new StreamSubscriber<T>(emitter);
        emitter.onCompletion(subscriber::cancel);
        emitter.onTimeout(subscriber::cancel);
        emitter.onError(ignored -> subscriber.cancel());
        publisher.subscribe(subscriber);
        return emitter;
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
