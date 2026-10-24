// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
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
        var applicationType = types.BasePackage + ".Application";
        var code = new JavaCodeBuilder(package).Line($$"""
            @org.springframework.boot.test.context.SpringBootTest(classes = {{applicationType}}.class)
            @org.springframework.test.annotation.DirtiesContext
            class {{name}}SubscriptionTest {
                @org.springframework.beans.factory.annotation.Autowired
                private org.axonframework.messaging.queryhandling.gateway.QueryGateway gateway;
                @org.springframework.beans.factory.annotation.Autowired
                private org.axonframework.common.configuration.Configuration configuration;
                @org.springframework.beans.factory.annotation.Autowired
                private {{projector}} projector;
                @org.springframework.beans.factory.annotation.Autowired
                @org.springframework.beans.factory.annotation.Qualifier({{JavaCodeBuilder.StringLiteral(modelType)}})
                private {{types.BasePackage}}.runtime.ReadModelStore<Object, {{modelType}}> store;

                @org.junit.jupiter.api.Test
                void shouldDeliverInitialSnapshotAndUpdateAndReleaseRegistration() throws Exception {
                    store.clear();
                    var initial = new java.util.concurrent.CountDownLatch(1);
                    var changed = new java.util.concurrent.CountDownLatch(1);
                    var values = new java.util.concurrent.CopyOnWriteArrayList<{{name}}.Result>();
                    var subscription = new java.util.concurrent.atomic.AtomicReference<java.util.concurrent.Flow.Subscription>();
                    var error = new java.util.concurrent.atomic.AtomicReference<Throwable>();
                    {{name}}Subscription.results(gateway, new {{name}}()).subscribe(new java.util.concurrent.Flow.Subscriber<{{name}}.Result>() {
                        @Override public void onSubscribe(java.util.concurrent.Flow.Subscription value) { subscription.set(value); value.request(2); }
                        @Override public void onNext({{name}}.Result value) {
                            values.add(value);
                            if (values.size() == 1) initial.countDown(); else changed.countDown();
                        }
                        @Override public void onError(Throwable failure) { error.set(failure); initial.countDown(); changed.countDown(); }
                        @Override public void onComplete() { changed.countDown(); }
                    });
                    try {
                        org.junit.jupiter.api.Assertions.assertTrue(initial.await(10, java.util.concurrent.TimeUnit.SECONDS), "Initial query snapshot timed out");
                        org.junit.jupiter.api.Assertions.assertNull(error.get());
                        org.junit.jupiter.api.Assertions.assertEquals(1, values.size());
                        org.junit.jupiter.api.Assertions.assertTrue(values.getFirst().rows().isEmpty());
                        configuration.getComponent(org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory.class).create()
                            .executeWithResult(processing -> {
                                projector.on({{AxonViewEvents.Expression(context, types, given)}}, org.axonframework.messaging.queryhandling.QueryUpdateEmitter.forContext(processing));
                                return java.util.concurrent.CompletableFuture.completedFuture(null);
                            }).get(10, java.util.concurrent.TimeUnit.SECONDS);
                        org.junit.jupiter.api.Assertions.assertTrue(changed.await(10, java.util.concurrent.TimeUnit.SECONDS), "Live query update timed out");
                        org.junit.jupiter.api.Assertions.assertNull(error.get());
                        org.junit.jupiter.api.Assertions.assertEquals(2, values.size());
                        org.junit.jupiter.api.Assertions.assertFalse(values.getLast().rows().isEmpty());
                    } finally {
                        var active = subscription.get();
                        if (active != null) active.cancel();
                    }
                    var remaining = configuration.getComponent(org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory.class).create()
                        .executeWithResult(processing -> java.util.concurrent.CompletableFuture.completedFuture(
                            org.axonframework.messaging.queryhandling.QueryUpdateEmitter.forContext(processing)
                                .emitAndCount({{name}}.class, query -> true, {{name}}.Result::invalidation).orElseThrow()))
                        .get(10, java.util.concurrent.TimeUnit.SECONDS);
                    org.junit.jupiter.api.Assertions.assertEquals(0, remaining);
                }
            }
            """).Build();
        context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}SubscriptionTest.java", code, [query.Id]));
    }
}
