// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class LiveQuerySubscription
{
    internal static void Emit(AxonEmissionContext context, string package, SemanticKeyedQuery query, string modelType)
    {
        var name = JavaNames.Type(query.Name);
        var payload = query.Cardinality == SemanticQueryCardinality.Many ? "result.rows()" : "result.rows().stream().findFirst()";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Subscription").Declare("Bridge");
        QueryModelReference.ReserveLocal(code, package, modelType, name, name + "Subscription", "Bridge");
        var flow = code.Reference("java.util.concurrent.Flow");
        var gateway = code.Reference("org.axonframework.messaging.queryhandling.gateway.QueryGateway");
        var source = code.Line($$"""
            public final class {{name}}Subscription {
                private {{name}}Subscription() {}

                public static {{flow}}.Publisher<{{name}}.Result> results({{gateway}} gateway, {{name}} query) {
                    return results(gateway, query, source -> source);
                }

                static {{flow}}.Publisher<{{name}}.Result> results({{gateway}} gateway, {{name}} query,
                        {{code.Reference("java.util.function.UnaryOperator")}}<{{code.Reference("org.reactivestreams.Publisher")}}<{{name}}.Result>> observe) {
                    return subscriber -> observe.apply(gateway.subscriptionQuery(query, {{name}}.Result.class))
                        .subscribe(new Bridge(gateway, query, subscriber));
                }

                public static {{flow}}.Publisher<{{code.Reference("java.lang.Object")}}> values({{gateway}} gateway, {{name}} query) {
                    return subscriber -> results(gateway, query).subscribe(new {{flow}}.Subscriber<{{name}}.Result>() {
                        @Override public void onSubscribe({{flow}}.Subscription subscription) { subscriber.onSubscribe(subscription); }
                        @Override public void onNext({{name}}.Result result) { subscriber.onNext({{payload}}); }
                        @Override public void onError({{code.Reference("java.lang.Throwable")}} failure) { subscriber.onError(failure); }
                        @Override public void onComplete() { subscriber.onComplete(); }
                    });
                }

                private static final class Bridge implements {{code.Reference("org.reactivestreams.Subscriber")}}<{{name}}.Result>, {{flow}}.Subscription {
                    private final {{gateway}} gateway;
                    private final {{name}} query;
                    private final {{flow}}.Subscriber<? super {{name}}.Result> downstream;
                    private {{code.Reference("org.reactivestreams.Subscription")}} upstream;
                    private {{code.Reference("java.util.concurrent.CompletableFuture")}}<{{name}}.Result> pending;
                    private long demand;
                    private boolean fetching;
                    private boolean closed;
                    private boolean completed;

                    private Bridge({{gateway}} gateway, {{name}} query, {{flow}}.Subscriber<? super {{name}}.Result> downstream) {
                        this.gateway = gateway;
                        this.query = query;
                        this.downstream = downstream;
                    }

                    @Override
                    public synchronized void onSubscribe({{code.Reference("org.reactivestreams.Subscription")}} subscription) {
                        if (upstream != null || closed) { subscription.cancel(); return; }
                        upstream = subscription;
                        downstream.onSubscribe(this);
                    }

                    @Override
                    public synchronized void request(long count) {
                        if (closed) return;
                        if (count <= 0) { onError(new {{code.Reference("java.lang.IllegalArgumentException")}}("Subscription demand must be positive")); return; }
                        demand = count > {{code.Reference("java.lang.Long")}}.MAX_VALUE - demand ? {{code.Reference("java.lang.Long")}}.MAX_VALUE : demand + count;
                        pump();
                    }

                    private void pump() {
                        if (!closed && !fetching && demand > 0 && upstream != null) {
                            fetching = true;
                            upstream.request(1);
                        }
                    }

                    @Override
                    public synchronized void onNext({{name}}.Result result) {
                        if (closed) return;
                        if (!result.invalidated()) { deliver(result); return; }
                        pending = gateway.query(query, {{name}}.Result.class);
                        pending.whenComplete((snapshot, failure) -> {
                            if (failure != null) onError(failure);
                            else deliver(snapshot);
                        });
                    }

                    private synchronized void deliver({{name}}.Result result) {
                        if (closed) return;
                        pending = null;
                        if (demand != {{code.Reference("java.lang.Long")}}.MAX_VALUE) demand--;
                        fetching = false;
                        downstream.onNext(result);
                        if (completed) { closed = true; downstream.onComplete(); }
                        else pump();
                    }

                    @Override
                    public synchronized void onError({{code.Reference("java.lang.Throwable")}} failure) {
                        if (closed) return;
                        cancel();
                        downstream.onError(failure);
                    }

                    @Override
                    public synchronized void onComplete() {
                        if (closed) return;
                        completed = true;
                        if (pending == null) { closed = true; downstream.onComplete(); }
                    }

                    @Override
                    public synchronized void cancel() {
                        if (closed) return;
                        closed = true;
                        if (upstream != null) upstream.cancel();
                        if (pending != null) pending.cancel(false);
                    }
                }
            }
            """).Build();
        ViewJavaTypes.Add(context, package, name + "Subscription", source, query.Id);
    }
}
