// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ReducerProjection(AxonEmissionContext context, ViewJavaTypes types)
{
    internal void Emit(LocatedSemanticSlice located, SemanticReadModel model, SemanticReducer reducer, ReducerBodies.Result? admission = null)
    {
        admission ??= ReducerBodies.Admission(context, located, reducer);
        if (admission.Blocked is { } blocked)
        {
            context.Degradation.Unsupported(admission.BlockedCode, model.Id, blocked);
            if (admission.BlockedCode == AxonRefusalCodes.CompositeKey)
            {
                var stubPackage = types.Package(located);
                var name = JavaNames.Type(model.Name) + "Reducer";
                var stub = new JavaCodeBuilder(stubPackage).Declare(name)
                    .UnsupportedIntent("reduce events into the read model: " + blocked, model.Id.ToString(), admission.BlockedCode)
                    .Block($"public class {name}", _ => { });
                context.AddJava(stubPackage, name, stub.Build(), sources: model.Id);
            }
            return;
        }
        var package = types.Package(located);
        if (reducer.Transitions.IsEmpty)
            context.Degradation.Unsupported(AxonRefusalCodes.Reduction, model.Id, "This reducer declares no transition body or observed event; there is no transition to render.", "reduce events into the read model", model.Id.ToString());
        EmitReducer(package, model, reducer, admission);
    }

    void EmitTransition(SemanticReadModel model, SemanticReducerTransition transition, ReducerBodies.Transition admission, JavaCodeBuilder code)
    {
        code.Block($"private {JavaNames.Type(model.Name)} reduce({types.EventType(transition.EventContract, code)} event, @{code.Reference("org.jspecify.annotations.Nullable")} {JavaNames.Type(model.Name)} current, {code.Reference("org.axonframework.messaging.eventhandling.EventMessage")} message)", method =>
        {
            if (admission.Placed)
            {
                method.RawBody(admission.Body);
                context.ConsumedImplementations.Add(transition.RequirementId);
                return;
            }
            var reason = admission.Reason + " Transition not implemented: events are ignored until a supported body is authored. A null current leaves the view absent.";
            context.Degradation.Unsupported(AxonRefusalCodes.Reduction, model.Id, reason, "reduce this event and current state into a new read model or delete it", model.Id.ToString());
            method.UnsupportedIntent("reduce this event and current state: " + reason, model.Id.ToString(), AxonRefusalCodes.Reduction)
                .Line("return current;");
        });
    }

    void EmitReducer(string package, SemanticReadModel model, SemanticReducer reducer, ReducerBodies.Result admission)
    {
        var name = JavaNames.Type(model.Name);
        var entity = ReadModelEntities.EntityName(model);
        var repository = ReadModelEntities.RepositoryName(model);
        var updates = new ProjectionUpdates(context, types, model.Id);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(name + "Reducer").Declare(entity).Declare(repository)
            .DiffersFromScreenplay("Reducer transitions are plain private methods of the projector; admitted Java bodies are placed verbatim; purity is the author's obligation.");
        updates.Claim(code, package);
        foreach (var transition in reducer.Transitions) types.EventType(transition.EventContract, code);
        var values = new ProjectionValues(context, types, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name + "Reducer")})")
            .Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(AxonProjectionsEmitter.ViewNamespace)})")
            .Block($"public class {name}Reducer", body =>
            {
                body.Line($"private final {repository} repository;")
                    .Block($"public {name}Reducer({repository} repository)", constructor => constructor.Line("this.repository = repository;"));
                if (reducer.Transitions.IsEmpty) body.UnsupportedIntent("reduce events into the read model", model.Id.ToString(), AxonRefusalCodes.Reduction);
                foreach (var transition in reducer.Transitions)
                {
                    EmitTransition(model, transition, admission.Transitions[transition.RequirementId], body);
                    var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Id == transition.EventContract);
                    new ProjectionHandlerBinding(context, types, model, package).Emit(
                        body,
                        types.EventType(transition.EventContract, body),
                        method => method.Line($"var key = {values.Source(@event)};")
                            .Line($"var next = reduce(event, repository.findById(key).map({entity}::toModel).orElse(null), message);")
                            .Line($"if (next == null) repository.deleteById(key); else repository.save(new {entity}(key, next));"),
                        message: true);
                }
            });
        context.AddJava(package, name + "Reducer", code.Build(), sources: model.Id);
    }
}
