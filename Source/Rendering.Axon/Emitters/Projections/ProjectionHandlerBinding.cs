// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionHandlerBinding(AxonEmissionContext context, ViewJavaTypes types, SemanticReadModel model, string package, bool synchronized = false, IReadOnlyList<string>? keys = null, string? scopeState = null)
{
    readonly ProjectionUpdates _updates = new(context, types, model.Id);

    /// <summary>
    /// Gets the keys to reserve before a multi-transition handler runs; none when a key cannot be evaluated, so the transition degrades on its own.
    /// </summary>
    /// <param name="keys">The evaluated key expressions, null where a key cannot be emitted.</param>
    /// <returns>The keys to reserve, or an empty list.</returns>
    internal static IReadOnlyList<string> PreReserved(IReadOnlyList<string?> keys) =>
        keys.Count >= 2 && keys.All(key => key is not null) ? [.. keys.OfType<string>()] : [];

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
        var scopeParameter = scopeState is null ? string.Empty : $", {store}<{body.Reference("java.lang.Object")}, {scopeState}> scope";
        var scopeArgument = scopeState is null ? string.Empty : ", scope";
        body.Line($"@{body.Reference("org.axonframework.messaging.eventhandling.annotation.EventHandler")}")
            .Block($"public void handle({eventType} event{updateParameter}, {processing} context)", method =>
            {
                // The message comes from the context so the handler keeps the dispatch shape of a context-free handler.
                if (message) method.Line($"var message = ({eventMessage}) {body.Reference("org.axonframework.messaging.core.Message")}.fromContext(context);");
                BindStore(method, messageArgument);
            })
            .Block($"public {modifier}void on({eventType} event{messageParameter}{updateParameter})", method => method.Line($"apply(event{arguments}, store{scopeArgument});"))
            .Block($"private void apply({eventType} event{messageParameter}{updateParameter}, {store}<{body.Reference("java.lang.Object")}, {JavaNames.Type(model.Name)}> store{scopeParameter})", method => Apply(method, transitions));
    }

    void BindStore(JavaCodeBuilder method, string messageArgument)
    {
        if (scopeState is not null && PersistentViewAdmission.Supports(context, model.Id))
            throw new InvalidOperationException($"Scoped projector state is not staged for persistent-admitted model {model.Id}.");
        var adapter = PersistentViewAdmission.Supports(context, model.Id) ? method.Reference(types.BasePackage + ".persistence.JpaReadModelStore") : null;
        var transaction = method.Reference(types.BasePackage + ".runtime.ReadModelTransaction");

        // The context-bound emitter already defers in-memory invalidations to AFTER_COMMIT, after the staged writes apply.
        var staged = $"apply(event{messageArgument}{(_updates.Enabled ? ", updates" : string.Empty)}, {transaction}.bind(store, context));";
        if (adapter is null)
        {
            BindMemory(method, messageArgument, transaction, staged);
            return;
        }
        method.Block($"if (store instanceof {adapter}<?>)", persistent => persistent.Line($"apply(event{messageArgument}{(_updates.Enabled ? ", updates" : string.Empty)}, {adapter}.bind(store, context));"));
        if (keys is { Count: >= 2 })
            method.Block("else", memory => BindMemory(memory, messageArgument, transaction, staged));
        else
            method.Line($"else {staged}");
    }

    void BindMemory(JavaCodeBuilder method, string messageArgument, string transaction, string staged)
    {
        if (scopeState is not null)
        {
            // The scope state is staged beside the rows and reserved first, so units of work on one projector serialize without a monitor.
            method.Line($"var state = {transaction}.bind(scope, context);")
                .Line($"{transaction}.reserve(state, {method.Reference("java.util.List")}.of(\"scope\"));")
                .Line($"apply(event{messageArgument}{(_updates.Enabled ? ", updates" : string.Empty)}, {transaction}.bind(store, context), state);");
            return;
        }
        if (keys is not { Count: >= 2 })
        {
            method.Line(staged);
            return;
        }
        method.Line($"var bound = {transaction}.bind(store, context);")
            .Line($"{transaction}.reserve(bound, {method.Reference("java.util.List")}.of({string.Join(", ", keys)}));")
            .Line($"apply(event{messageArgument}{(_updates.Enabled ? ", updates" : string.Empty)}, bound);");
    }

    void Apply(JavaCodeBuilder method, Action<JavaCodeBuilder> transitions)
    {
        transitions(method);

        // A scoped body notifies only when it modified a row.
        if (scopeState is null) _updates.Notify(method, package);
    }
}
