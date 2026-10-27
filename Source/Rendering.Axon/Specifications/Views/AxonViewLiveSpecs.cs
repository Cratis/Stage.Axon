// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal static class AxonViewLiveSpecs
{
    internal static void Emit(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticKeyedQuery query, LocatedSemanticSlice owner, SemanticReadModel model)
    {
        if (query.Argument is not null || query.KeyParts.Length > 0 || query.Filters.Length > 0 || query.Cardinality != SemanticQueryCardinality.Many || query.Authorization is not null) return;
        var projection = owner.Slice.Projections.FirstOrDefault(candidate => candidate.ReadModel == model.Id);
        if (projection is null) return;
        var given = owner.Slice.Specifications.SelectMany(specification => specification.GivenEvents)
            .FirstOrDefault(fact => ProjectionTransitions.From(projection).Any(from => from.EventContract == fact.EventContract));
        if (given is null) return;
        var package = types.Package(located);
        var name = JavaNames.Type(query.Name);
        var modelType = types.Package(owner) + "." + JavaNames.Type(model.Name);
        var projector = types.Package(owner) + "." + JavaNames.Type(projection.Name) + "Projector";
        var code = new JavaCodeBuilder(package).Declare(name + "SubscriptionTest").Declare(name).Declare(name + "Subscription");
        foreach (var samePackageModel in located.Slice.ReadModels) code.Declare(JavaNames.Type(samePackageModel.Name));
        var modelName = JavaNames.Type(model.Name);
        var projectorName = JavaNames.Type(projection.Name) + "Projector";
        if (package == types.Package(owner))
        {
            code.Declare(projectorName);
        }
        else
        {
            modelName = code.Reference(modelType);
            projectorName = code.Reference(projector);
        }
        var updates = new ProjectionUpdates(context, types, model.Id);
        string EventArguments(JavaCodeBuilder builder)
        {
            new ViewSpecificationClaims(context, types).Event(given, builder);
            return AxonViewEvents.Expression(context, types, given, builder) + ProjectionHandlerShape.Arguments(projection, updates, updates.Enabled ? builder.Reference("org.axonframework.messaging.queryhandling.QueryUpdateEmitter") + ".forContext(processing)" : "null");
        }
        var eventArguments = EventArguments(code);
        var storeType = code.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"));
        var applicationType = code.Reference(types.BasePackage + ".Application");
        var source = code.Line($$"""
            @{{code.Reference("org.springframework.boot.test.context.SpringBootTest")}}(classes = {{applicationType}}.class)
            @{{code.Reference("org.springframework.test.annotation.DirtiesContext")}}
            class {{name}}SubscriptionTest {
                @{{code.Reference("org.springframework.beans.factory.annotation.Autowired")}}
                private {{code.Reference("org.axonframework.messaging.queryhandling.gateway.QueryGateway")}} gateway;
                @{{code.Reference("org.springframework.beans.factory.annotation.Autowired")}}
                private {{code.Reference("org.axonframework.common.configuration.Configuration")}} configuration;
                @{{code.Reference("org.springframework.beans.factory.annotation.Autowired")}}
                private {{projectorName}} projector;
                @{{code.Reference("org.springframework.beans.factory.annotation.Autowired")}}
                @{{code.Reference("org.springframework.beans.factory.annotation.Qualifier")}}({{JavaCodeBuilder.StringLiteral(modelType)}})
                private {{storeType}}<Object, {{modelName}}> store;

                @{{code.Reference("org.junit.jupiter.api.Test")}}
                void shouldDeliverInitialSnapshotAndUpdateAndCancelAxonPublisher() throws Exception {
                    store.clear();
                    var initial = new {{code.Reference("java.util.concurrent.CountDownLatch")}}(1);
                    var changed = new {{code.Reference("java.util.concurrent.CountDownLatch")}}(1);
                    var cancelled = new {{code.Reference("java.util.concurrent.CountDownLatch")}}(1);
                    var values = new {{code.Reference("java.util.concurrent.CopyOnWriteArrayList")}}<{{name}}.Result>();
                    var subscription = new {{code.Reference("java.util.concurrent.atomic.AtomicReference")}}<{{code.Reference("java.util.concurrent.Flow")}}.Subscription>();
                    var error = new {{code.Reference("java.util.concurrent.atomic.AtomicReference")}}<Throwable>();
                    {{name}}Subscription.results(gateway, new {{name}}(), source -> target -> source.subscribe(new {{code.Reference("org.reactivestreams.Subscriber")}}<{{name}}.Result>() {
                        @Override public void onSubscribe({{code.Reference("org.reactivestreams.Subscription")}} upstream) {
                            target.onSubscribe(new {{code.Reference("org.reactivestreams.Subscription")}}() {
                                @Override public void request(long amount) { upstream.request(amount); }
                                @Override public void cancel() { upstream.cancel(); cancelled.countDown(); }
                            });
                        }
                        @Override public void onNext({{name}}.Result value) { target.onNext(value); }
                        @Override public void onError(Throwable failure) { target.onError(failure); }
                        @Override public void onComplete() { target.onComplete(); }
                    })).subscribe(new {{code.Reference("java.util.concurrent.Flow")}}.Subscriber<{{name}}.Result>() {
                        @Override public void onSubscribe({{code.Reference("java.util.concurrent.Flow")}}.Subscription value) { subscription.set(value); value.request(2); }
                        @Override public void onNext({{name}}.Result value) {
                            values.add(value);
                            if (values.size() == 1) initial.countDown(); else changed.countDown();
                        }
                        @Override public void onError(Throwable failure) { error.set(failure); initial.countDown(); changed.countDown(); }
                        @Override public void onComplete() { changed.countDown(); }
                    });
                    try {
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(initial.await(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS), "Initial query snapshot timed out");
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertNull(error.get());
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(1, values.size());
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(values.getFirst().rows().isEmpty());
                        configuration.getComponent({{code.Reference("org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory")}}.class).create()
                            .executeWithResult(processing -> {
                                projector.on({{eventArguments}});
                                return {{code.Reference("java.util.concurrent.CompletableFuture")}}.completedFuture(null);
                            }).get(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS);
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(changed.await(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS), "Live query update timed out");
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertNull(error.get());
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertEquals(2, values.size());
                        {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertFalse(values.getLast().rows().isEmpty());
                    } finally {
                        var active = subscription.get();
                        if (active != null) active.cancel();
                    }
                    {{code.Reference("org.junit.jupiter.api.Assertions")}}.assertTrue(cancelled.await(10, {{code.Reference("java.util.concurrent.TimeUnit")}}.SECONDS), "Cancellation did not reach the real Axon subscription publisher");
                }
            }
            """).Build();
        context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}SubscriptionTest.java", source, [query.Id]));
        AxonViewHttpLiveSpecs.Emit(context, types, located, query, modelType, projector, EventArguments);
    }
}
