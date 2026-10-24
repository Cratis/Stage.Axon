// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class LiveQuerySubscription
{
    internal static void Emit(AxonEmissionContext context, string package, SemanticKeyedQuery query)
    {
        var name = JavaNames.Type(query.Name);
        var payload = query.Cardinality == SemanticQueryCardinality.Many ? "result.rows()" : "result.rows().stream().findFirst()";
        var source = new JavaCodeBuilder(package).Import("java.util.concurrent.Flow")
            .Import("org.axonframework.messaging.queryhandling.gateway.QueryGateway")
            .Line($$"""
            public final class {{name}}Subscription {
                private {{name}}Subscription() {}

                public static Flow.Publisher<{{name}}.Result> results(QueryGateway gateway, {{name}} query) {
                    return results(gateway, query, source -> source);
                }

                static Flow.Publisher<{{name}}.Result> results(QueryGateway gateway, {{name}} query,
                        java.util.function.UnaryOperator<org.reactivestreams.Publisher<{{name}}.Result>> observe) {
                    return subscriber -> observe.apply(gateway.subscriptionQuery(query, {{name}}.Result.class))
                        .subscribe(new Bridge(gateway, query, subscriber));
                }

                public static Flow.Publisher<Object> values(QueryGateway gateway, {{name}} query) {
                    return subscriber -> results(gateway, query).subscribe(new Flow.Subscriber<{{name}}.Result>() {
                        @Override public void onSubscribe(Flow.Subscription subscription) { subscriber.onSubscribe(subscription); }
                        @Override public void onNext({{name}}.Result result) { subscriber.onNext({{payload}}); }
                        @Override public void onError(Throwable failure) { subscriber.onError(failure); }
                        @Override public void onComplete() { subscriber.onComplete(); }
                    });
                }

                private static final class Bridge implements org.reactivestreams.Subscriber<{{name}}.Result>, Flow.Subscription {
                    private final QueryGateway gateway;
                    private final {{name}} query;
                    private final Flow.Subscriber<? super {{name}}.Result> downstream;
                    private org.reactivestreams.Subscription upstream;
                    private java.util.concurrent.CompletableFuture<{{name}}.Result> pending;
                    private long demand;
                    private boolean fetching;
                    private boolean closed;
                    private boolean completed;

                    private Bridge(QueryGateway gateway, {{name}} query, Flow.Subscriber<? super {{name}}.Result> downstream) {
                        this.gateway = gateway;
                        this.query = query;
                        this.downstream = downstream;
                    }

                    @Override
                    public synchronized void onSubscribe(org.reactivestreams.Subscription subscription) {
                        if (upstream != null || closed) { subscription.cancel(); return; }
                        upstream = subscription;
                        downstream.onSubscribe(this);
                    }

                    @Override
                    public synchronized void request(long count) {
                        if (closed) return;
                        if (count <= 0) { onError(new IllegalArgumentException("Subscription demand must be positive")); return; }
                        demand = count > Long.MAX_VALUE - demand ? Long.MAX_VALUE : demand + count;
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
                        if (demand != Long.MAX_VALUE) demand--;
                        fetching = false;
                        downstream.onNext(result);
                        if (completed) { closed = true; downstream.onComplete(); }
                        else pump();
                    }

                    @Override
                    public synchronized void onError(Throwable failure) {
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
