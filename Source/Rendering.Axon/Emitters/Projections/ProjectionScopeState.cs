// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionScopeState(ViewJavaTypes types, ProjectionScopeNode root, IReadOnlyList<SemanticEventContract> events)
{
    internal static string Deferred(SemanticEventContract @event) => "Deferred" + JavaNames.Type(@event.Name);

    internal static string Join(ProjectionScopeNode node, int index) => "Join" + node.Name + index;

    internal static bool Defers(ProjectionScopeNode root, SemanticEventContract @event) => root.All.Any(node => node.Identity is not null &&
        (node.Scope.From.Any(from => from.EventContract == @event.Id) || node.Scope.Removals.Any(removal => removal.EventContract == @event.Id)));

    internal void Claim(JavaCodeBuilder code)
    {
        code.Declare("StoredScopeMessage").Declare("DeferredScopeEvent");
        foreach (var @event in events.Where(@event => Defers(root, @event))) code.Declare(Deferred(@event));
        foreach (var node in root.All)
            for (var index = 0; index < node.Scope.Joins.Length; index++) code.Declare(Join(node, index));
    }

    internal void Emit(JavaCodeBuilder code)
    {
        Message(code);
        DeferredEntries(code);
        foreach (var node in root.All)
        {
            for (var index = 0; index < node.Scope.Joins.Length; index++)
            {
                var @event = events.Single(candidate => candidate.Id == node.Scope.Joins[index].EventContract);
                code.Line($"public record {Join(node, index)}({types.EventType(@event.Id, code)} event, StoredScopeMessage message) {{}}");
            }
        }
        Snapshot(code);
    }

    static void Message(JavaCodeBuilder code)
    {
        var map = $"{code.Reference("java.util.Map")}<{code.Reference("java.lang.String")}, {code.Reference("java.lang.String")}>";
        var message = code.Reference("org.axonframework.messaging.eventhandling.EventMessage");
        code.Block($"public record StoredScopeMessage(boolean absent, {code.Reference("java.lang.String")} identifier, {code.Reference("java.time.Instant")} timestamp, {map} metadata)", record => record
            .Block("public StoredScopeMessage", constructor => constructor.Line($"metadata = {code.Reference("java.util.Collections")}.unmodifiableMap(new {code.Reference("java.util.LinkedHashMap")}<>(metadata));"))
            .Block($"static StoredScopeMessage capture({message} message)", method => method
                .Line($"return message == null ? new StoredScopeMessage(true, null, null, {code.Reference("java.util.Map")}.of()) : new StoredScopeMessage(false, message.identifier(), message.timestamp(), message.metadata());"))
            .Block($"{message} restore({code.Reference("java.lang.Object")} event)", method => method
                .Line("if (absent) return null;")
                .Line($"var type = new {code.Reference("org.axonframework.messaging.core.annotation.AnnotationMessageTypeResolver")}().resolve(event.getClass()).orElseThrow();")
                .Line($"return new {code.Reference("org.axonframework.messaging.eventhandling.GenericEventMessage")}(identifier, type, event, metadata, timestamp);")));
    }

    void DeferredEntries(JavaCodeBuilder code)
    {
        var deferred = events.Where(@event => Defers(root, @event)).ToArray();
        if (deferred.Length > 0)
        {
            var info = code.Reference("com.fasterxml.jackson.annotation.JsonTypeInfo");
            var subtypes = code.Reference("com.fasterxml.jackson.annotation.JsonSubTypes");
            code.Line($"@{info}(use = {info}.Id.NAME, property = \"kind\")")
                .Line($"@{subtypes}({{{string.Join(", ", deferred.Select(@event => $"@{subtypes}.Type(value = {Deferred(@event)}.class, name = {JavaCodeBuilder.StringLiteral(Deferred(@event))})"))}}})")
                .Line($"public sealed interface DeferredScopeEvent permits {string.Join(", ", deferred.Select(Deferred))} {{}}");
        }
        else
        {
            code.Line("public sealed interface DeferredScopeEvent permits NoDeferredScopeEvent {}")
                .Line("public record NoDeferredScopeEvent() implements DeferredScopeEvent {}");
        }
        foreach (var @event in deferred)
            code.Line($"public record {Deferred(@event)}({types.EventType(@event.Id, code)} event, StoredScopeMessage message) implements DeferredScopeEvent {{}}");
        code.Block($"private boolean applyDeferred(DeferredScopeEvent entry, {Store(code, root.Type)} store)", method =>
        {
            foreach (var @event in deferred)
                method.Line($"if (entry instanceof {Deferred(@event)} value) return project{JavaNames.Type(@event.Name)}(value.event(), value.message().restore(value.event()), store);");
            method.Line($"throw new {code.Reference("java.lang.IllegalArgumentException")}(\"Unknown deferred scope event\");");
        });
    }

    void Snapshot(JavaCodeBuilder code)
    {
        var state = ScopedProjectionEmitter.StateType(root);
        var list = code.Reference("java.util.List");
        var caches = root.All.SelectMany(node => Enumerable.Range(0, node.Scope.Joins.Length).Select(index => (Name: ScopedProjectionEmitter.Cache(node, index), Type: Join(node, index)))).ToArray();
        var components = string.Join(", ", new[] { $"{list}<{code.Reference("java.lang.Object")}> keys", $"{list}<DeferredScopeEvent> pending" }
            .Concat(caches.Select(cache => $"{code.Reference("java.util.Map")}<{code.Reference("java.lang.String")}, {cache.Type}> {cache.Name}")));
        code.Line($"private final {Store(code, state)} scope = new {code.Reference(AxonRuntimePackage.Type(types.BasePackage, "InMemoryReadModelStore"))}<>();")
            .Line($"private {state} applied;").Line("private boolean clean;")
            .Block($"public record {state}({components})", record => record.Block($"public {state}", constructor =>
            {
                constructor.Line($"keys = {list}.copyOf(keys);").Line($"pending = {list}.copyOf(pending);");
                foreach (var cache in caches) constructor.Line($"{cache.Name} = {code.Reference("java.util.Collections")}.unmodifiableMap(new {code.Reference("java.util.LinkedHashMap")}<>({cache.Name}));");
            }))
            .Block($"private void restore({state} state)", method =>
            {
                method.Line("if (clean && state == applied) return;").Line("keys.clear();").Line("pending.clear();");
                foreach (var cache in caches) method.Line($"{cache.Name}.clear();");
                method.Block("if (state != null)", restored =>
                {
                    restored.Line("keys.addAll(state.keys());").Line("pending.addAll(state.pending());");
                    foreach (var cache in caches) restored.Line($"{cache.Name}.putAll(state.{cache.Name}());");
                }).Line("applied = state;");
            })
            .Block($"private {state} snapshot()", method => method.Line("clean = true;")
                .Line($"return applied = new {state}(new {code.Reference("java.util.ArrayList")}<>(keys), pending{string.Concat(caches.Select(cache => ", " + cache.Name))});"));
    }

    string Store(JavaCodeBuilder code, string value) => $"{code.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"))}<{code.Reference("java.lang.Object")}, {value}>";
}
