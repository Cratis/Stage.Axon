// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class LiveQueryStreams
{
    internal static void Emit(AxonEmissionContext context, string package, SemanticKeyedQuery query)
    {
        var name = JavaNames.Type(query.Name) + "Streams";
        var code = new JavaCodeBuilder(package).Line($$"""
            @org.springframework.stereotype.Component({{JavaCodeBuilder.StringLiteral(package + "." + name)}})
            public final class {{name}} implements org.springframework.context.ApplicationListener<org.springframework.context.event.ContextClosedEvent> {
                private final java.util.Set<Session> sessions = java.util.concurrent.ConcurrentHashMap.newKeySet();
                private final java.util.concurrent.atomic.AtomicBoolean closing = new java.util.concurrent.atomic.AtomicBoolean();

                public org.springframework.web.servlet.mvc.method.annotation.SseEmitter open(java.util.concurrent.Flow.Publisher<Object> publisher) {
                    var session = new Session();
                    sessions.add(session);
                    if (closing.get()) session.stop();
                    var emitter = {{context.BasePackage}}.runtime.SseBridge.stream(downstream -> publisher.subscribe(session.forwardTo(downstream)));
                    session.attach(emitter);
                    return emitter;
                }

                int activeCount() { return sessions.size(); }

                @Override
                public void onApplicationEvent(org.springframework.context.event.ContextClosedEvent event) {
                    closing.set(true);
                    sessions.forEach(Session::stop);
                }

                private final class Session {
                    private final java.util.concurrent.atomic.AtomicReference<java.util.concurrent.Flow.Subscription> subscription = new java.util.concurrent.atomic.AtomicReference<>();
                    private final java.util.concurrent.atomic.AtomicBoolean cancelled = new java.util.concurrent.atomic.AtomicBoolean();
                    private volatile org.springframework.web.servlet.mvc.method.annotation.SseEmitter emitter;

                    private java.util.concurrent.Flow.Subscriber<Object> forwardTo(java.util.concurrent.Flow.Subscriber<? super Object> downstream) {
                        return new java.util.concurrent.Flow.Subscriber<>() {
                            @Override public void onSubscribe(java.util.concurrent.Flow.Subscription value) {
                                if (!subscription.compareAndSet(null, value)) { value.cancel(); return; }
                                downstream.onSubscribe(new java.util.concurrent.Flow.Subscription() {
                                    @Override public void request(long amount) { if (!cancelled.get()) value.request(amount); }
                                    @Override public void cancel() { Session.this.cancel(); }
                                });
                                if (cancelled.get()) value.cancel();
                            }
                            @Override public void onNext(Object value) { if (!cancelled.get()) downstream.onNext(value); }
                            @Override public void onError(Throwable failure) { cancel(); downstream.onError(failure); }
                            @Override public void onComplete() { cancel(); downstream.onComplete(); }
                        };
                    }

                    private void attach(org.springframework.web.servlet.mvc.method.annotation.SseEmitter value) {
                        emitter = value;
                        value.onCompletion(this::cancel);
                        value.onTimeout(this::cancel);
                        value.onError(failure -> cancel());
                        if (cancelled.get() || closing.get()) value.complete();
                    }

                    private void cancel() {
                        if (!cancelled.compareAndSet(false, true)) return;
                        sessions.remove(this);
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
            """);
        context.AddJava(package, name, code.Build(), sources: query.Id);
    }
}
