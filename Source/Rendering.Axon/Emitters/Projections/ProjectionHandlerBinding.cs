// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionHandlerBinding(AxonEmissionContext context, ViewJavaTypes types, SemanticReadModel model, string package)
{
    readonly ProjectionUpdates _updates = new(context, types, model.Id);

    /// <summary>
    /// Emits the Axon event handler and the entry point that applies one event to the repository, then invalidates the live queries.
    /// </summary>
    /// <param name="body">The projector body.</param>
    /// <param name="eventType">The handled event type.</param>
    /// <param name="transitions">Emits the repository reads and writes for the event.</param>
    /// <param name="message">Whether the transitions read the event message.</param>
    internal void Emit(JavaCodeBuilder body, string eventType, Action<JavaCodeBuilder> transitions, bool message = false)
    {
        var processing = body.Reference("org.axonframework.messaging.core.unitofwork.ProcessingContext");
        var updateParameter = _updates.ParameterFor(body);
        var eventMessage = message ? body.Reference("org.axonframework.messaging.eventhandling.EventMessage") : string.Empty;
        var messageParameter = message ? $", {eventMessage} message" : string.Empty;
        var arguments = (message ? ", message" : string.Empty) + (_updates.Enabled ? ", updates" : string.Empty);
        body.Line($"@{body.Reference("org.axonframework.messaging.eventhandling.annotation.EventHandler")}")
            .Block($"public void handle({eventType} event{updateParameter}, {processing} context)", method =>
            {
                // The message comes from the context so the handler keeps the dispatch shape of a context-free handler.
                if (message) method.Line($"var message = ({eventMessage}) {body.Reference("org.axonframework.messaging.core.Message")}.fromContext(context);");
                method.Line($"on(event{arguments});");
            })
            .Block($"public void on({eventType} event{messageParameter}{updateParameter})", method =>
            {
                transitions(method);
                _updates.Notify(method, package);
            });
    }
}
