// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ReducerProjection(AxonEmissionContext context, ViewJavaTypes types)
{
    internal void Emit(LocatedSemanticSlice located, SemanticReadModel model, SemanticReducer reducer)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(model.Name);
        if (reducer.Transitions.IsEmpty)
            context.Degradation.Unsupported(AxonRefusalCodes.Reduction, model.Id, "This reducer declares no transition body or observed event; there is no transition to render.", "reduce events into the read model", model.Id.ToString());
        var contract = new JavaCodeBuilder(package)
            .DiffersFromScreenplay("the reducer is an Axon event projector delegating to a replaceable Spring reducer bean; implementation bodies are not translated.")
            .Block($"public interface {name}Reducer", body =>
            {
                if (reducer.Transitions.IsEmpty) body.UnsupportedIntent("reduce events into the read model", model.Id.ToString(), AxonRefusalCodes.Reduction);
                foreach (var transition in reducer.Transitions) EmitTransition(model, transition, body);
            });
        context.AddJava(package, name + "Reducer", contract.Build(), sources: model.Id);
        EmitConfiguration(package, name, model.Id);
        EmitProjector(package, model, reducer);
    }

    void EmitTransition(SemanticReadModel model, SemanticReducerTransition transition, JavaCodeBuilder code)
    {
        var requirement = context.Request.ImplementationRequirements.FirstOrDefault(candidate => candidate.RequirementId == transition.RequirementId);
        var hasBody = requirement is not null && context.Request.ImplementationContents.Any(content => content.Key == transition.RequirementId && !string.IsNullOrWhiteSpace(content.Value));
        var reason = hasBody
            ? "Reducer implementation bodies are not translated to Java; implement the generated reducer interface as a Spring bean."
            : "This reducer has no available transition body: an event and result type do not specify how to calculate state or deletion. Implement the generated reducer interface before delivering events.";
        context.Degradation.Unsupported(AxonRefusalCodes.Reduction, model.Id, reason, "reduce this event and current state into a new read model or delete it", model.Id.ToString());
        code.Block($"default {JavaNames.Type(model.Name)} reduce({types.EventType(transition.EventContract)} event, {JavaNames.Type(model.Name)} current, org.axonframework.messaging.eventhandling.EventMessage message)", method => method
            .UnsupportedIntent("reduce this event and current state into a new read model or delete it", model.Id.ToString(), AxonRefusalCodes.Reduction)
            .Line($"throw new UnsupportedOperationException({JavaCodeBuilder.StringLiteral(AxonRefusalCodes.Reduction + ": " + reason)});"));
    }

    void EmitConfiguration(string package, string name, SemanticId id)
    {
        var code = new JavaCodeBuilder(package)
            .Line($"@org.springframework.context.annotation.Configuration({JavaCodeBuilder.StringLiteral(package + "." + name + "ReducerConfiguration")})")
            .Block($"public class {name}ReducerConfiguration", body => body
                .Line($"@org.springframework.context.annotation.Bean({JavaCodeBuilder.StringLiteral(package + "." + name + "Reducer")})")
                .Line($"@org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean({name}Reducer.class)")
                .Block($"public {name}Reducer {JavaNames.Member(name)}Reducer()", method => method.Line($"return new {name}Reducer() {{}};")));
        context.AddJava(package, name + "ReducerConfiguration", code.Build(), sources: id);
    }

    void EmitProjector(string package, SemanticReadModel model, SemanticReducer reducer)
    {
        var name = JavaNames.Type(model.Name);
        var updates = new ProjectionUpdates(context, types, model.Id);
        var values = new ProjectionValues(context, types);
        var code = new JavaCodeBuilder(package).Import(context.BasePackage + ".runtime.ReadModelStore")
            .Line($"@org.springframework.stereotype.Component({JavaCodeBuilder.StringLiteral(package + "." + name + "ReducerProjector")})")
            .Block($"public class {name}ReducerProjector", body =>
            {
                body.Line($"private final ReadModelStore<Object, {name}> store;").Line($"private final {name}Reducer reducer;")
                    .Block($"public {name}ReducerProjector(@org.springframework.beans.factory.annotation.Qualifier({JavaCodeBuilder.StringLiteral(package + "." + name)}) ReadModelStore<Object, {name}> store, {name}Reducer reducer)", constructor => constructor.Line("this.store = store;").Line("this.reducer = reducer;"));
                foreach (var transition in reducer.Transitions)
                {
                    var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Id == transition.EventContract);
                    body.Line("@org.axonframework.messaging.eventhandling.annotation.EventHandler")
                        .Block($"public synchronized void on({types.EventType(transition.EventContract)} event, org.axonframework.messaging.eventhandling.EventMessage message{updates.Parameter})", method =>
                        {
                            method.Line($"var key = {values.Source(@event)};")
                                .Line("var next = reducer.reduce(event, store.get(key).orElse(null), message);")
                                .Line("if (next == null) store.remove(key); else store.put(key, next);");
                            updates.Notify(method);
                        });
                }
            });
        context.AddJava(package, name + "ReducerProjector", code.Build(), sources: model.Id);
    }
}
