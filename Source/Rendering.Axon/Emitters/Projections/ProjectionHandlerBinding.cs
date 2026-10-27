// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionHandlerBinding(AxonEmissionContext context, ViewJavaTypes types, SemanticReadModel model, string package)
{
    readonly ProjectionUpdates _updates = new(context, types, model.Id);

    internal void Emit(JavaCodeBuilder body, string eventType, Action<JavaCodeBuilder> transitions, bool message = false)
    {
        if (PersistentViewAdmission.Supports(context, model.Id))
        {
            EmitContextual(body, eventType, transitions, message);
            return;
        }
        body.Line($"@{body.Reference("org.axonframework.messaging.eventhandling.annotation.EventHandler")}")
            .Block($"public void on({eventType} event{_updates.Parameter})", method => Apply(method, transitions));
    }

    void EmitContextual(JavaCodeBuilder body, string eventType, Action<JavaCodeBuilder> transitions, bool message)
    {
        var adapter = body.Reference(types.BasePackage + ".persistence.JpaReadModelStore");
        var processing = body.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext");
        var store = body.Reference(types.BasePackage + ".runtime.ReadModelStore");
        var messageParameter = message ? $", {body.Reference("org.axonframework.messaging.eventhandling.EventMessage")} message" : string.Empty;
        var messageArgument = message ? ", message" : string.Empty;
        var arguments = messageArgument + (_updates.Enabled ? ", updates" : string.Empty);
        body.Line($"@{body.Reference("org.axonframework.messaging.eventhandling.annotation.EventHandler")}")
            .Block($"public void handle({eventType} event{messageParameter}{_updates.Parameter}, {processing} context)", method =>
            {
                method.Block($"if (!(store instanceof {adapter}<?>))", memory => memory.Line($"on(event{arguments});").Line("return;"))
                    .Line($"apply(event{messageArgument}{_updates.DefaultArgument}, {adapter}.bind(store, context));");
                if (_updates.Enabled)
                    method.Block("context.runOnAfterCommit(ignored ->", callback => _updates.Notify(callback, package)).Line(");");
            })
            .Block($"public void on({eventType} event{messageParameter}{_updates.Parameter})", method => method.Line($"apply(event{arguments}, store);"))
            .Block($"private void apply({eventType} event{messageParameter}{_updates.Parameter}, {store}<{body.Reference("java.lang.Object")}, {JavaNames.Type(model.Name)}> store)", method => Apply(method, transitions));
    }

    void Apply(JavaCodeBuilder method, Action<JavaCodeBuilder> transitions)
    {
        transitions(method);
        _updates.Notify(method, package);
    }
}
