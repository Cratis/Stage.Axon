// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class LiveQueryStreams
{
    internal static void Emit(AxonEmissionContext context, string package, SemanticKeyedQuery query, string modelType)
    {
        var name = JavaNames.Type(query.Name) + "Streams";
        var code = new JavaCodeBuilder(package).Declare(JavaNames.Type(query.Name)).Declare(name).Declare("Session");
        QueryModelReference.ReserveLocal(code, package, modelType, JavaNames.Type(query.Name), name, "Session");
        var flow = code.Reference("java.util.concurrent.Flow");
        var atomicBoolean = code.Reference("java.util.concurrent.atomic.AtomicBoolean");
        var emitter = code.Reference("org.springframework.web.servlet.mvc.method.annotation.SseEmitter");
        var closed = code.Reference("org.springframework.context.event.ContextClosedEvent");
        code.Line($$"""
            @{{code.Reference("org.springframework.stereotype.Component")}}({{JavaCodeBuilder.StringLiteral(package + "." + name)}})
            public final class {{name}} implements {{code.Reference("org.springframework.context.ApplicationListener")}}<{{closed}}> {
                private final {{code.Reference("java.util.Set")}}<Session> sessions = {{code.Reference("java.util.concurrent.ConcurrentHashMap")}}.newKeySet();
                private final {{atomicBoolean}} closing = new {{atomicBoolean}}();

                public {{emitter}} open({{flow}}.Publisher<{{code.Reference("java.lang.Object")}}> publisher) {
                    var session = new Session();
                    sessions.add(session);
                    if (closing.get()) session.stop();
                    var emitter = {{code.Reference(context.BasePackage + ".runtime.SseBridge")}}.stream(downstream -> publisher.subscribe(session.forwardTo(downstream)));
                    session.attach(emitter);
                    return emitter;
                }

                int activeCount() { return sessions.size(); }

                @Override
                public void onApplicationEvent({{closed}} event) {
                    closing.set(true);
                    sessions.forEach(Session::stop);
                }

                private final class Session {
                    private final {{code.Reference("java.util.concurrent.atomic.AtomicReference")}}<{{flow}}.Subscription> subscription = new {{code.Reference("java.util.concurrent.atomic.AtomicReference")}}<>();
                    private final {{atomicBoolean}} cancelled = new {{atomicBoolean}}();
                    private volatile {{emitter}} emitter;

                    private {{flow}}.Subscriber<{{code.Reference("java.lang.Object")}}> forwardTo({{flow}}.Subscriber<? super {{code.Reference("java.lang.Object")}}> downstream) {
                        return new {{flow}}.Subscriber<>() {
                            @Override public void onSubscribe({{flow}}.Subscription value) {
                                if (!subscription.compareAndSet(null, value)) { value.cancel(); return; }
                                downstream.onSubscribe(new {{flow}}.Subscription() {
                                    @Override public void request(long amount) { if (!cancelled.get()) value.request(amount); }
                                    @Override public void cancel() { Session.this.cancel(); }
                                });
                                if (cancelled.get()) value.cancel();
                            }
                            @Override public void onNext({{code.Reference("java.lang.Object")}} value) { if (!cancelled.get()) downstream.onNext(value); }
                            @Override public void onError({{code.Reference("java.lang.Throwable")}} failure) { cancel(); downstream.onError(failure); }
                            @Override public void onComplete() { cancel(); downstream.onComplete(); }
                        };
                    }

                    private void attach({{emitter}} value) {
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
