// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionHandlerBinding(AxonEmissionContext context, ViewJavaTypes types, SemanticReadModel model, string package, bool synchronized = false)
{
    readonly ProjectionUpdates _updates = new(context, types, model.Id);

    internal void Emit(JavaCodeBuilder body, string eventType, Action<JavaCodeBuilder> transitions, bool message = false)
    {
        EmitContextual(body, eventType, transitions, message);
    }

    void EmitContextual(JavaCodeBuilder body, string eventType, Action<JavaCodeBuilder> transitions, bool message)
    {
        var processing = body.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext");
        var store = body.Reference(types.BasePackage + ".runtime.ReadModelStore");
        var modifier = synchronized ? "synchronized " : string.Empty;
        var updateParameter = _updates.ParameterFor(body);
        var eventMessage = message ? body.Reference("org.axonframework.messaging.eventhandling.EventMessage") : string.Empty;
        var messageParameter = message ? $", {eventMessage} message" : string.Empty;
        var messageArgument = message ? ", message" : string.Empty;
        var arguments = messageArgument + (_updates.Enabled ? ", updates" : string.Empty);
        body.Line($"@{body.Reference("org.axonframework.messaging.eventhandling.annotation.EventHandler")}")
            .Block($"public void handle({eventType} event{updateParameter}, {processing} context)", method =>
            {
                // The message comes from the context so the handler keeps the dispatch shape of a context-free handler.
                if (message) method.Line($"var message = ({eventMessage}) {body.Reference("org.axonframework.messaging.core.Message")}.fromContext(context);");
                BindStore(method, messageArgument);
            })
            .Block($"public {modifier}void on({eventType} event{messageParameter}{updateParameter})", method => method.Line($"apply(event{arguments}, store);"))
            .Block($"private void apply({eventType} event{messageParameter}{updateParameter}, {store}<{body.Reference("java.lang.Object")}, {JavaNames.Type(model.Name)}> store)", method => Apply(method, transitions));
    }

    void BindStore(JavaCodeBuilder method, string messageArgument)
    {
        var adapter = PersistentViewAdmission.Supports(context, model.Id) ? method.Reference(types.BasePackage + ".persistence.JpaReadModelStore") : null;
        var transaction = method.Reference(types.BasePackage + ".runtime.ReadModelTransaction");

        // The context-bound emitter already defers in-memory invalidations to AFTER_COMMIT, after the staged writes apply.
        var staged = $"apply(event{messageArgument}{(_updates.Enabled ? ", updates" : string.Empty)}, {transaction}.bind(store, context));";
        if (adapter is null)
        {
            method.Line(staged);
            return;
        }
        method.Block($"if (store instanceof {adapter}<?>)", persistent =>
        {
            persistent.Line($"apply(event{messageArgument}{_updates.DefaultArgument}, {adapter}.bind(store, context));");
            if (_updates.Enabled)
                persistent.Block("context.runOnAfterCommit(ignored ->", callback => _updates.Notify(callback, package)).Line(");");
        }).Line($"else {staged}");
    }

    void Apply(JavaCodeBuilder method, Action<JavaCodeBuilder> transitions)
    {
        transitions(method);
        _updates.Notify(method, package);
    }
}
