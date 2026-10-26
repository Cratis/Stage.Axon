// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ScopedProjectionEmitter(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
{
    readonly ProjectionUpdates _updates = new(context, types, model.Id);
    readonly ProjectionScopeNode _root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));

    internal static string Cache(ProjectionScopeNode node, int index) => "join" + node.Name + index;

    internal void Emit()
    {
        var name = JavaNames.Type(projection.Name) + "Projector";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(_root.Type).Declare(ChangeType(_root));
        foreach (var property in _root.All.SelectMany(node => node.Properties)) types.ClaimProjectionType(property.Type, code);
        _updates.Claim(code, package);
        foreach (var @event in Events()) types.EventType(@event.Id, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name)})")
            .Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(package)})")
            .Block($"public class {name}", body =>
        {
            Fields(body, name);
            var methods = new ProjectionScopeMethods(context, types, _root, projection.Id).WithReferences(body);
            foreach (var @event in Events())
            {
                Handler(body, @event, methods);
                foreach (var node in _root.All) methods.Emit(body, node, @event);
            }
        });
        ViewJavaTypes.Add(context, package, name, code.Build(), projection.Id);
    }

    internal static string ChangeType(ProjectionScopeNode root) => root.Type == "ScopeChange" ? "ScopeChangeResult" : "ScopeChange";

    static IEnumerable<ProjectionScopeNode> RootAndNested(ProjectionScopeNode node) => new[] { node }.Concat(node.Nested.SelectMany(RootAndNested));

    void Fields(JavaCodeBuilder code, string name)
    {
        code.Line($"private final {code.Reference(types.BasePackage + ".runtime.ReadModelStore")}<{code.Reference("java.lang.Object")}, {_root.Type}> store;")
            .Line($"private final {code.Reference("java.time.Clock")} clock;")
            .Line($"private final {code.Reference("java.util.Set")}<{code.Reference("java.lang.Object")}> keys = new {code.Reference("java.util.LinkedHashSet")}<>();")
            .Line($"private final {code.Reference("java.util.List")}<{code.Reference("java.util.function.BooleanSupplier")}> pending = new {code.Reference("java.util.ArrayList")}<>();")
            .Line($"private record {ChangeType(_root)}<T>(T value, boolean touched, boolean ancestors) {{}}")
            .DiffersFromScreenplay("projection join indexes are replay-built; restore them by replaying events after restarting a persistent store.");
        foreach (var node in _root.All)
        {
            for (var index = 0; index < node.Scope.Joins.Length; index++)
                code.Line($"private final {code.Reference("java.util.Map")}<{code.Reference("java.lang.String")}, {code.Reference("java.util.function.UnaryOperator")}<{code.Reference(node.Type)}>> {Cache(node, index)} = new {code.Reference("java.util.HashMap")}<>();");
        }
        code.Line($"@{code.Reference("org.springframework.beans.factory.annotation.Autowired")}")
            .Block($"public {name}(@{code.Reference("org.springframework.beans.factory.annotation.Qualifier")}({JavaCodeBuilder.StringLiteral(package + "." + _root.Type)}) {code.Reference(types.BasePackage + ".runtime.ReadModelStore")}<{code.Reference("java.lang.Object")}, {_root.Type}> store, {code.Reference("java.time.Clock")} clock)", constructor => constructor.Line("this.store = store;").Line("this.clock = clock;"))
            .Block($"public {name}({code.Reference(types.BasePackage + ".runtime.ReadModelStore")}<{code.Reference("java.lang.Object")}, {_root.Type}> store)", constructor => constructor.Line($"this(store, {constructor.Reference("java.time.Clock")}.systemUTC());"));
    }

    void Handler(JavaCodeBuilder code, SemanticEventContract @event, ProjectionScopeMethods methods)
    {
        var eventType = types.EventType(@event.Id, code);
        var method = "project" + JavaNames.Type(@event.Name);
        code.Line($"@{code.Reference("org.axonframework.messaging.eventhandling.annotation.EventHandler")}").Block($"public synchronized void on({eventType} event, {code.Reference("org.axonframework.messaging.eventhandling.EventMessage")} message{_updates.ParameterFor(code)})", body =>
            {
                foreach (var node in _root.All) methods.Caches(body, node, @event);
                var deferred = _root.All.Any(node => node.Identity is not null && (node.Scope.From.Any(from => from.EventContract == @event.Id) || node.Scope.Removals.Any(removal => removal.EventContract == @event.Id)));
                body.Line($"boolean modified = {method}(event, message);");
                if (deferred) body.Line($"if (!modified) pending.add(() -> {method}(event, message));");
                body.Line("boolean progressed;").Block("do", loop => loop
                    .Line("progressed = false;").Line("var iterator = pending.iterator();")
                    .Block("while (iterator.hasNext())", iteration => iteration.Block("if (iterator.next().getAsBoolean())", found => found.Line("iterator.remove();").Line("progressed = true;").Line("modified = true;"))))
                    .Line("while (progressed);");
                if (_updates.Enabled) body.Block("if (modified)", updates => _updates.Notify(updates, package));
            }).Block($"private boolean {method}({eventType} event, {code.Reference("org.axonframework.messaging.eventhandling.EventMessage")} message)", body =>
        {
            var values = new ProjectionValues(context, types, body);
            body.Line($"var candidates = new {body.Reference("java.util.LinkedHashSet")}<>(keys);");
            foreach (var node in RootAndNested(_root))
            {
                foreach (var from in node.Scope.From.Where(from => from.EventContract == @event.Id))
                    if (values.Key(from.Key, @event) is { } key) body.Line($"candidates.add({key});");
            }
            if (_root.Scope.Every is { SubscribesToAllEvents: true }) body.Line($"candidates.add({values.Source(@event)});");
            body.Line("boolean modified = false;").Block("for (var key : candidates)", loop =>
            {
                loop.Line($"var touched = new {loop.Reference("java.util.concurrent.atomic.AtomicBoolean")}();")
                    .Line("var updated = store.update(key, current -> {")
                    .Line($"    var change = {methods.Method(_root, @event)}(event, message, key, key, current);")
                    .Line("    touched.set(change.touched());")
                    .Line("    return change.touched() ? change.value() : current;")
                    .Line("});")
                    .Block("if (touched.get())", changed => changed.Line("modified = true;").Line("if (updated == null) keys.remove(key); else keys.add(key);"));
            }).Line("return modified;");
        });
    }

    IEnumerable<SemanticEventContract> Events()
    {
        var ids = _root.All.SelectMany(node => node.Scope.From.Select(from => from.EventContract)
            .Concat(node.Scope.Joins.Select(join => join.EventContract)).Concat(node.Scope.Removals.Select(removal => removal.EventContract))
            .Concat(node.Scope.JoinRemovals.Select(removal => removal.EventContract))).ToHashSet();
        return ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events)
            .Where(@event => ids.Contains(@event.Id) || _root.Scope.Every is { SubscribesToAllEvents: true }).DistinctBy(@event => @event.Id);
    }
}
