// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

/// <summary>
/// Emits the pure record transforms of a scoped projection: one method per scope node and event that maps the current record
/// of the node to its next record, and one method per join that applies the stored join event to a record.
/// </summary>
internal sealed class ProjectionScopeMethods(AxonEmissionContext context, ViewJavaTypes types, ProjectionScopeNode root, SemanticId owner, IReadOnlyList<ProjectionJoinSlot> slots, JavaCodeBuilder? references = null)
{
    readonly ProjectionRecords _records = new(context.Request.Model.Application, types, references);
    readonly ProjectionScopeMappings _mappings = new(context, types, owner, references);
    readonly ProjectionValues _values = references is null ? new(context, types) : new(context, types, references);
    readonly JavaCodeBuilder? _code = references;

    internal static string ChangeType(ProjectionScopeNode root) => root.Type == "ScopeChange" ? "ScopeChangeResult" : "ScopeChange";

    internal static string JoinMethod(ProjectionJoinSlot slot) => "apply" + char.ToUpperInvariant(slot.Member[0]) + slot.Member[1..];

    internal ProjectionScopeMethods WithReferences(JavaCodeBuilder code) => new(context, types, root, owner, slots, code);

    internal string Method(ProjectionScopeNode node, SemanticEventContract @event) => "apply" + node.Name + JavaNames.Type(@event.Name);

    internal void EmitJoin(JavaCodeBuilder code, ProjectionJoinSlot slot)
    {
        var eventType = EventType(slot.Join.EventContract);
        var @event = ScopeSelectionEvent(slot.Join.EventContract);
        code.Block($"private {Reference(slot.Node.Type)} {JoinMethod(slot)}({Reference(slot.Node.Type)} state, {eventType} event, {Reference("org.axonframework.messaging.eventhandling.EventMessage", "EventMessage")} message)", body =>
        {
            _mappings.Apply(body, slot.Node, slot.Join.Mappings, @event);
            body.Line("return state;");
        });
    }

    internal void Emit(JavaCodeBuilder code, ProjectionScopeNode node, SemanticEventContract @event)
    {
        var change = ChangeType(root);
        var text = Reference("java.lang.String", "String");
        code.Block($"private {change}<{Reference(node.Type)}> {Method(node, @event)}({EventType(@event.Id)} event, {Reference("org.axonframework.messaging.eventhandling.EventMessage", "EventMessage")} message, {text} key, {text} parentKey, {Reference(node.Type)} current)", body =>
        {
            var creates = Creation(node, @event);
            body.Line($"if (current == null && !({creates})) return new {change}<>(null, false, false);")
                .Line($"var state = current == null ? {_records.Empty(node.Type, node.Properties)} : current;")
                .Line("boolean touched = false;").Line("boolean ancestors = false;");
            foreach (var from in node.Scope.From.Where(from => from.EventContract == @event.Id))
            {
                body.Block($"if ({Matches(from.Key, from.ParentKey, @event, node)})", selected =>
                {
                    _mappings.Identify(selected, node, from, @event);
                    _mappings.Apply(selected, node, from.Mappings, @event);
                    _mappings.Apply(selected, node, node.Scope.Every?.Mappings ?? [], @event);
                    Hydrate(selected, node);
                    selected.Line("touched = true;").Line("ancestors = true;");
                });
            }
            Joins(body, node, @event);
            foreach (var removal in node.Scope.Removals.Where(removal => removal.EventContract == @event.Id))
                body.Line($"if ({Matches(removal.Key, removal.ParentKey, @event, node)}) return new {change}<>(null, true, true);");
            if (node.Identity is not null)
            {
                foreach (var removal in node.Scope.JoinRemovals.Where(removal => removal.EventContract == @event.Id))
                    body.Line($"if ({Reference("java.util.Objects")}.equals(key, {_values.Key(removal.Key, @event)})) return new {change}<>(null, true, false);");
            }
            Descendants(body, node, @event);
            if (node == root && node.Scope.Every is { SubscribesToAllEvents: true } && !Known(@event))
            {
                _mappings.Apply(body, node, node.Scope.Every.Mappings, @event);
                body.Line("touched = true;");
            }
            body.Line($"return new {change}<>(state, touched, ancestors);");
        });
    }

    void Joins(JavaCodeBuilder code, ProjectionScopeNode node, SemanticEventContract @event)
    {
        foreach (var slot in slots.Where(slot => slot.Node == node && slot.Join.EventContract == @event.Id))
        {
            var join = slot.Join;
            var property = node.Properties.First(property => property.Id == (node.Identity ?? join.On));
            var value = $"state.{JavaNames.Member(property.Name)}()";
            var on = types.Unwrap(property.Type, value);
            var key = _values.Key(join.Key ?? SemanticProjectionKey.EventSourceIdentity, @event);
            code.Block($"if (current != null && {value} != null && {Reference("java.util.Objects")}.equals({Reference("java.lang.String", "String")}.valueOf({on}), {Reference("java.lang.String", "String")}.valueOf({key})))", selected =>
            {
                selected.Line($"state = {JoinMethod(slot)}(state, event, message);");
                _mappings.Apply(selected, node, node.Scope.Every?.Mappings ?? [], @event);
                selected.Line("touched = true;");
            });
        }
    }

    void Hydrate(JavaCodeBuilder code, ProjectionScopeNode node)
    {
        foreach (var slot in slots.Where(slot => slot.Node == node))
        {
            var property = node.Properties.First(property => property.Id == (node.Identity ?? slot.Join.On));
            var value = $"state.{JavaNames.Member(property.Name)}()";
            var key = types.Unwrap(property.Type, value);
            code.Block($"if ({value} != null)", hydrate => hydrate
                .Line($"var joined = joins.findById({JavaCodeBuilder.StringLiteral(slot.Prefix)} + {Reference("java.lang.String", "String")}.valueOf({key}));")
                .Line($"if (joined.isPresent() && joined.get().{slot.Member}() != null) state = {JoinMethod(slot)}(state, joined.get().{slot.Member}(), null);"));
        }
    }

    void Descendants(JavaCodeBuilder code, ProjectionScopeNode node, SemanticEventContract @event)
    {
        code.Line("boolean childTouched = false;").Line("boolean childAncestors = false;");
        foreach (var nested in node.Nested)
        {
            var property = nested.Property!;
            var variable = JavaNames.Member(nested.Name);
            code.Line($"var {variable} = {Method(nested, @event)}(event, message, key, parentKey, state.{JavaNames.Member(property.Name)}());")
                .Block($"if ({variable}.touched())", changed => changed
                    .Line($"state = {_records.Set(node.Type, node.Properties, [property.Id], "state", variable + ".value()")};")
                    .Line("childTouched = true;").Line($"childAncestors |= {variable}.ancestors();"));
        }
        foreach (var child in node.Children) Children(code, node, child, @event);
        if (node.Scope.Every is { IncludeChildren: true } every)
            code.Block("if (!touched && childAncestors)", everyBlock => _mappings.Apply(everyBlock, node, every.Mappings, @event));
        code.Line("touched |= childTouched;").Line("ancestors |= childAncestors;");
    }

    void Children(JavaCodeBuilder code, ProjectionScopeNode parent, ProjectionScopeNode child, SemanticEventContract @event)
    {
        var property = child.Property!;
        var identity = child.Properties.First(candidate => candidate.Id == child.Identity);
        var member = JavaNames.Member(property.Name);
        var variable = JavaNames.Member(child.Name);
        var change = ChangeType(root);
        code.Line($"var {variable} = new {Reference("java.util.ArrayList")}<{Reference(child.Type)}>();")
            .Block($"if (state.{member}() != null)", items => items.Block($"for (var item : state.{member}())", item => item
                .Line($"var identity = {Reference("java.lang.String", "String")}.valueOf({types.Unwrap(identity.Type, $"item.{JavaNames.Member(identity.Name)}()")});")
                .Line($"var changed = {Method(child, @event)}(event, message, identity, key, item);")
                .Line($"if (changed.value() != null) {variable}.add(changed.value());")
                .Line("childTouched |= changed.touched();").Line("childAncestors |= changed.ancestors();")));
        foreach (var from in child.Scope.From.Where(from => from.EventContract == @event.Id))
        {
            var key = _values.Key(from.Key, @event);
            var parentKey = _values.Key(from.ParentKey ?? SemanticProjectionKey.EventSourceIdentity, @event);
            if (parent.Scope.From.Any(candidate => candidate.EventContract == @event.Id)) parentKey = "key";
            code.Block($"if ({Reference("java.util.Objects")}.equals(key, {parentKey}) && {variable}.stream().noneMatch(item -> {Reference("java.util.Objects")}.equals({Reference("java.lang.String", "String")}.valueOf({types.Unwrap(identity.Type, $"item.{JavaNames.Member(identity.Name)}()")}), {Reference("java.lang.String", "String")}.valueOf({key}))))", create => create
                .Line($"var added = {Method(child, @event)}(event, message, {key}, key, null);")
                .Line($"if (added.value() != null) {variable}.add(added.value());")
                .Line("childTouched |= added.touched();").Line("childAncestors |= added.ancestors();"));
        }
        code.Line($"state = {_records.Set(parent.Type, parent.Properties, [property.Id], "state", Reference("java.util.List") + ".copyOf(" + variable + ")")};");
    }

    string EventType(SemanticId id) => _code is null ? types.EventType(id) : types.EventType(id, _code);

    SemanticEventContract ScopeSelectionEvent(SemanticId id) => Sdk.ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(candidate => candidate.Id == id);

    string Reference(string type, string? legacy = null) => _code?.Reference(type) ?? legacy ?? type;

    string Matches(SemanticProjectionKey key, SemanticProjectionKey? parentKey, SemanticEventContract @event, ProjectionScopeNode node)
    {
        var result = $"{Reference("java.util.Objects")}.equals(key, {_values.Key(key, @event) ?? "null"})";
        if (node.Identity is not null)
            result += $" && {Reference("java.util.Objects")}.equals(parentKey, {_values.Key(parentKey ?? SemanticProjectionKey.EventSourceIdentity, @event) ?? "null"})";
        return result;
    }

    string Creation(ProjectionScopeNode node, SemanticEventContract @event)
    {
        var conditions = node.Scope.From.Where(from => from.EventContract == @event.Id).Select(from => Matches(from.Key, from.ParentKey, @event, node))
            .Concat(node.Nested.Select(nested => Creation(nested, @event)))
            .Concat(node.Children.SelectMany(child => child.Scope.From.Where(from => from.EventContract == @event.Id).Select(from => $"{Reference("java.util.Objects")}.equals(key, {_values.Key(from.ParentKey ?? SemanticProjectionKey.EventSourceIdentity, @event) ?? "null"})"))).ToList();
        if (node == root && node.Scope.Every is { SubscribesToAllEvents: true } && !Known(@event)) conditions.Add("true");
        return string.Join(" || ", conditions.DefaultIfEmpty("false"));
    }

    bool Known(SemanticEventContract @event) => root.All.Any(node => node.Scope.From.Any(from => from.EventContract == @event.Id) || node.Scope.Joins.Any(join => join.EventContract == @event.Id) || node.Scope.Removals.Any(removal => removal.EventContract == @event.Id) || node.Scope.JoinRemovals.Any(removal => removal.EventContract == @event.Id));
}
