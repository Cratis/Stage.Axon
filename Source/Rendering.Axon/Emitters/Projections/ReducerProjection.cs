// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ReducerProjection(AxonEmissionContext context, ViewJavaTypes types)
{
    internal void Emit(LocatedSemanticSlice located, SemanticReadModel model, SemanticReducer reducer, ReducerBodies.Result? admission = null)
    {
        admission ??= ReducerBodies.Admission(context, located, reducer);
        if (ReducerModelAvailability.Reason(reducer, admission) is { } blocked)
        {
            context.Degradation.Unsupported(AxonRefusalCodes.Reduction, model.Id, blocked);
            return;
        }
        var package = types.Package(located);
        var name = JavaNames.Type(model.Name);
        var contract = new JavaCodeBuilder(package).Declare(name).Declare(name + "Reducer")
            .DiffersFromScreenplay("the reducer delegates to a replaceable Spring bean. Admitted Java bodies are placed verbatim. Purity is the author's obligation; Axon does not verify it.")
            .Block($"public interface {name}Reducer", body =>
            {
                foreach (var transition in reducer.Transitions) EmitTransition(model, transition, admission.Transitions[transition.RequirementId], body);
            });
        context.AddJava(package, name + "Reducer", contract.Build(), sources: model.Id);
        EmitConfiguration(package, name, model.Id);
        EmitProjector(package, model, reducer);
    }

    void EmitTransition(SemanticReadModel model, SemanticReducerTransition transition, ReducerBodies.Transition admission, JavaCodeBuilder code)
    {
        code.Block($"default {JavaNames.Type(model.Name)} reduce({types.EventType(transition.EventContract, code)} event, @{code.Reference("org.jspecify.annotations.Nullable")} {JavaNames.Type(model.Name)} current, {code.Reference("org.axonframework.messaging.eventhandling.EventMessage")} message)", method =>
        {
            method.RawBody(admission.Body);
            context.ConsumedImplementations.Add(transition.RequirementId);
        });
    }

    void EmitConfiguration(string package, string name, SemanticId id)
    {
        var code = new JavaCodeBuilder(package).Declare(name + "Reducer").Declare(name + "ReducerConfiguration");
        code.Line($"@{code.Reference("org.springframework.context.annotation.Configuration")}({JavaCodeBuilder.StringLiteral(package + "." + name + "ReducerConfiguration")})")
            .Block($"public class {name}ReducerConfiguration", body => body
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({JavaCodeBuilder.StringLiteral(package + "." + name + "Reducer")})")
                .Line($"@{body.Reference("org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean")}({name}Reducer.class)")
                .Block($"public {name}Reducer {JavaNames.Member(name)}Reducer()", method => method.Line($"return new {name}Reducer() {{}};")));
        context.AddJava(package, name + "ReducerConfiguration", code.Build(), sources: id);
    }

    void EmitProjector(string package, SemanticReadModel model, SemanticReducer reducer)
    {
        var name = JavaNames.Type(model.Name);
        var updates = new ProjectionUpdates(context, types, model.Id);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Reducer").Declare(name + "ReducerProjector");
        var storeType = code.Reference(AxonRuntimePackage.Type(context, "ReadModelStore"));
        updates.Claim(code, package);
        foreach (var transition in reducer.Transitions) types.EventType(transition.EventContract, code);
        var values = new ProjectionValues(context, types, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name + "ReducerProjector")})")
            .Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(package)})")
            .Block($"public class {name}ReducerProjector", body =>
            {
                body.Line($"private final {storeType}<{body.Reference("java.lang.Object")}, {name}> store;").Line($"private final {name}Reducer reducer;")
                    .Block($"public {name}ReducerProjector(@{body.Reference("org.springframework.beans.factory.annotation.Qualifier")}({JavaCodeBuilder.StringLiteral(package + "." + name)}) {storeType}<{body.Reference("java.lang.Object")}, {name}> store, {name}Reducer reducer)", constructor => constructor.Line("this.store = store;").Line("this.reducer = reducer;"));
                foreach (var transition in reducer.Transitions)
                {
                    var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Id == transition.EventContract);
                    new ProjectionHandlerBinding(context, types, model, package, synchronized: true, sequence: new ProjectionSequencing(context, types, model).Reducer(@event, body)).Emit(
                        body,
                        types.EventType(transition.EventContract, body),
                        method => method.Line($"var key = {values.Source(@event)};")
                            .Line("var next = reducer.reduce(event, store.get(key).orElse(null), message);")
                            .Line("if (next == null) store.remove(key); else store.put(key, next);"),
                        message: true);
                }
            });
        context.AddJava(package, name + "ReducerProjector", code.Build(), sources: model.Id);
        var owner = ScopeSelection.Select(context.Request).First(candidate => candidate.Slice.Reducers.Any(candidateReducer => candidateReducer.ReadModel == model.Id));
        new ProjectorRollbackTests(context, types).Emit(owner, model, name + "ReducerProjector", $"new {name}ReducerProjector(store, new {name}Reducer() {{}})", [.. reducer.Transitions.Select(transition => transition.EventContract)]);
    }
}
