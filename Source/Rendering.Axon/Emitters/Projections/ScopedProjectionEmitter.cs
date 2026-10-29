// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

/// <summary>
/// Emits a projection with scoped state (every, nested scopes, children, joins, join removals and parented removals) as pure record transforms
/// over the typed repository of the read model: the row of an affected key is read, mapped to its next record and saved or deleted.
/// Nested scopes and children live in JSON columns of the row; joins hydrate from a projection-owned lookup table.
/// </summary>
internal sealed class ScopedProjectionEmitter(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
{
    readonly ProjectionScopeNode _root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));

    /// <summary>
    /// Whether the scoped projection can be emitted: the read model has a single string key, every scope resolves to a composite property and every key is renderable.
    /// </summary>
    /// <param name="context">The emission context.</param>
    /// <param name="types">The Java type resolver.</param>
    /// <param name="projection">The projection.</param>
    /// <param name="model">The projected read model.</param>
    /// <returns>True when the projection is renderable.</returns>
    internal static bool Supports(AxonEmissionContext context, ViewJavaTypes types, SemanticProjection projection, SemanticReadModel model)
    {
        var application = context.Request.Model.Application;
        if (ReadModelKeys.IsComposite(application, model) || !ProjectionScopeNode.Resolvable(application, projection, model)) return false;
        var root = ProjectionScopeNode.Create(application, types, projection, model, JavaNames.Type(model.Name));
        var values = new ProjectionValues(context, types);
        var events = ScopeSelection.Locate(application).SelectMany(slice => slice.Slice.Events).ToDictionary(@event => @event.Id);
        bool Renders(SemanticId contract, params SemanticProjectionKey?[] keys) => events.TryGetValue(contract, out var @event) && keys.All(key => key is null || values.Key(key, @event) is not null);
        return root.All.All(node => node.Scope.From.All(from => Renders(from.EventContract, from.Key, from.ParentKey))
            && node.Scope.Removals.All(removal => Renders(removal.EventContract, removal.Key, removal.ParentKey))
            && node.Scope.JoinRemovals.All(removal => Renders(removal.EventContract, removal.Key))
            && node.Scope.Joins.All(join => Renders(join.EventContract, join.Key ?? SemanticProjectionKey.EventSourceIdentity)));
    }

    internal static IEnumerable<ProjectionScopeNode> RootAndNested(ProjectionScopeNode node) => new[] { node }.Concat(node.Nested.SelectMany(RootAndNested));

    internal static bool Observes(SemanticProjectionScope scope, SemanticEventContract @event) => scope.From.Any(from => from.EventContract == @event.Id) || scope.Removals.Any(removal => removal.EventContract == @event.Id);

    internal void Emit()
    {
        var name = JavaNames.Type(projection.Name) + "Projector";
        var slots = ProjectionJoinSlot.All(_root);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(_root.Type).Declare(ReadModelEntities.EntityName(model)).Declare(ReadModelEntities.RepositoryName(model)).Declare(ProjectionScopeMethods.ChangeType(_root));
        if (slots.Count > 0) code.Declare(ProjectionJoinEntities.EntityName(projection)).Declare(ProjectionJoinEntities.RepositoryName(projection));
        foreach (var property in _root.All.SelectMany(node => node.Properties)) types.ClaimProjectionType(property.Type, code);
        new ProjectionUpdates(context, types, model.Id).Claim(code, package);
        foreach (var @event in Events()) types.EventType(@event.Id, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name)})")
            .Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(package)})")
            .Block($"public class {name}", body =>
            {
                Fields(body, name, slots);
                var methods = new ProjectionScopeMethods(context, types, _root, projection.Id, slots).WithReferences(body);
                foreach (var @event in Events())
                {
                    new ProjectionHandlerBinding(context, types, model, package).Emit(body, types.EventType(@event.Id, body), method => Transitions(method, @event, slots, methods), message: true);
                    foreach (var node in _root.All) methods.Emit(body, node, @event);
                }
                foreach (var slot in slots) methods.EmitJoin(body, slot);
            });
        ViewJavaTypes.Add(context, package, name, code.Build(), projection.Id);
        if (slots.Count > 0) ProjectionJoinEntities.Emit(context, types, package, projection, slots);
    }

    void Fields(JavaCodeBuilder code, string name, IReadOnlyList<ProjectionJoinSlot> slots)
    {
        var repository = ReadModelEntities.RepositoryName(model);
        var joins = slots.Count > 0 ? ProjectionJoinEntities.RepositoryName(projection) : null;
        var clock = code.Reference("java.time.Clock");
        var parameters = $"{repository} repository" + (joins is null ? string.Empty : $", {joins} joins");
        var arguments = "repository" + (joins is null ? string.Empty : ", joins");
        code.Line($"private final {repository} repository;");
        if (joins is not null) code.Line($"private final {joins} joins;");
        code.Line($"private final {clock} clock;")
            .Line($"private record {ProjectionScopeMethods.ChangeType(_root)}<T>(T value, boolean touched, boolean ancestors) {{}}")
            .Line($"@{code.Reference("org.springframework.beans.factory.annotation.Autowired")}")
            .Block($"public {name}({parameters}, {clock} clock)", constructor =>
            {
                constructor.Line("this.repository = repository;");
                if (joins is not null) constructor.Line("this.joins = joins;");
                constructor.Line("this.clock = clock;");
            })
            .Block($"public {name}({parameters})", constructor => constructor.Line($"this({arguments}, {constructor.Reference("java.time.Clock")}.systemUTC());"));
    }

    void Transitions(JavaCodeBuilder body, SemanticEventContract @event, IReadOnlyList<ProjectionJoinSlot> slots, ProjectionScopeMethods methods)
    {
        var values = new ProjectionValues(context, types, body);
        var text = body.Reference("java.lang.String");
        var modelName = JavaNames.Type(model.Name);
        var entity = ReadModelEntities.EntityName(model);
        foreach (var slot in slots.Where(slot => slot.Join.EventContract == @event.Id))
        {
            var key = values.Key(slot.Join.Key ?? SemanticProjectionKey.EventSourceIdentity, @event);
            body.Line($"joins.save(new {ProjectionJoinEntities.EntityName(projection)}({JavaCodeBuilder.StringLiteral(slot.Prefix)} + {text}.valueOf({key})).{slot.Member}(event));");
        }

        body.Line($"var keys = new {body.Reference("java.util.LinkedHashSet")}<{text}>();");
        foreach (var key in Keys(values, @event).Distinct()) body.Line($"keys.add({key});");
        var scan = Scans(@event) ? "for (var row : repository.findAll()) states.put(row.rowKey(), row.toModel());" : string.Empty;
        body.Line($"var states = new {body.Reference("java.util.LinkedHashMap")}<{text}, {modelName}>();")
            .Line(scan)
            .Line($"keys.forEach(candidate -> states.putIfAbsent(candidate, repository.findById(candidate).map({entity}::toModel).orElse(null)));")
            .Block("for (var entry : states.entrySet())", loop => loop
            .Line("var key = entry.getKey();")
            .Line($"var change = {methods.Method(_root, @event)}(event, message, key, key, entry.getValue());")
            .Line("if (!change.touched()) continue;")
            .Line($"if (change.value() == null) repository.deleteAllByIdInBatch({body.Reference("java.util.List")}.of(key));")
            .Line($"else repository.save(new {entity}(key, change.value()));"));
    }

    /// <summary>The row keys an event can create or reach through a key: root and nested keys, the parent keys of children and removals.</summary>
    IEnumerable<string> Keys(ProjectionValues values, SemanticEventContract @event)
    {
        foreach (var node in RootAndNested(_root))
        {
            foreach (var from in node.Scope.From.Where(from => from.EventContract == @event.Id)) yield return values.Key(from.Key, @event)!;
            foreach (var removal in node.Scope.Removals.Where(removal => removal.EventContract == @event.Id)) yield return values.Key(removal.Key, @event)!;
            foreach (var child in node.Children)
            {
                foreach (var from in child.Scope.From.Where(from => from.EventContract == @event.Id)) yield return values.Key(from.ParentKey ?? SemanticProjectionKey.EventSourceIdentity, @event)!;
                foreach (var removal in child.Scope.Removals.Where(removal => removal.EventContract == @event.Id)) yield return values.Key(removal.ParentKey ?? SemanticProjectionKey.EventSourceIdentity, @event)!;
            }
        }
        if (_root.Scope.Every is { SubscribesToAllEvents: true }) yield return values.Source(@event);
    }

    /// <summary>Whether the event reaches rows it cannot address by a key: joins, join removals, scopes below a child and the every scope of all events.</summary>
    bool Scans(SemanticEventContract @event) => _root.Scope.Every is { SubscribesToAllEvents: true } ||
        _root.All.Any(node => node.Scope.Joins.Any(join => join.EventContract == @event.Id) || node.Scope.JoinRemovals.Any(removal => removal.EventContract == @event.Id)) ||
        RootAndNested(_root).SelectMany(node => node.Children).SelectMany(child => child.All.Skip(1)).Any(node => Observes(node.Scope, @event));

    IEnumerable<SemanticEventContract> Events()
    {
        var ids = _root.All.SelectMany(node => node.Scope.From.Select(from => from.EventContract)
            .Concat(node.Scope.Joins.Select(join => join.EventContract)).Concat(node.Scope.Removals.Select(removal => removal.EventContract))
            .Concat(node.Scope.JoinRemovals.Select(removal => removal.EventContract))).ToHashSet();
        return ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events)
            .Where(@event => ids.Contains(@event.Id) || _root.Scope.Every is { SubscribesToAllEvents: true }).DistinctBy(@event => @event.Id);
    }
}
