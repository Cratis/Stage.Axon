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
        var code = QueryModelReference.Create(context, package).Declare(name).Declare(name + "Subscription");
        QueryModelReference.ReserveLocal(code, package, modelType, name, name + "Subscription");
        var flow = code.Reference("java.util.concurrent.Flow");
        var gateway = code.Reference("org.axonframework.messaging.queryhandling.gateway.QueryGateway");
        var live = code.Reference(context.BasePackage + ".runtime.LiveSnapshots");
        var source = code.Line($$"""
            public final class {{name}}Subscription {
                private {{name}}Subscription() {}

                public static {{flow}}.Publisher<{{name}}.Result> results({{gateway}} gateway, {{name}} query) {
                    return results(gateway, query, source -> source);
                }

                static {{flow}}.Publisher<{{name}}.Result> results({{gateway}} gateway, {{name}} query,
                        {{code.Reference("java.util.function.UnaryOperator")}}<{{code.Reference("org.reactivestreams.Publisher")}}<{{name}}.Result>> observe) {
                    return {{live}}.of(() -> observe.apply(gateway.subscriptionQuery(query, {{name}}.Result.class)), {{name}}.Result::invalidated, () -> gateway.query(query, {{name}}.Result.class));
                }

                public static {{flow}}.Publisher<{{code.Reference("java.lang.Object")}}> values({{gateway}} gateway, {{name}} query) {
                    return subscriber -> results(gateway, query).subscribe(new {{flow}}.Subscriber<{{name}}.Result>() {
                        @{{code.Reference("java.lang.Override")}} public void onSubscribe({{flow}}.Subscription subscription) { subscriber.onSubscribe(subscription); }
                        @{{code.Reference("java.lang.Override")}} public void onNext({{name}}.Result result) { subscriber.onNext({{payload}}); }
                        @{{code.Reference("java.lang.Override")}} public void onError({{code.Reference("java.lang.Throwable")}} failure) { subscriber.onError(failure); }
                        @{{code.Reference("java.lang.Override")}} public void onComplete() { subscriber.onComplete(); }
                    });
                }
            }
            """).Build();
        ViewJavaTypes.Add(context, package, name + "Subscription", source, query.Id);
    }
}
