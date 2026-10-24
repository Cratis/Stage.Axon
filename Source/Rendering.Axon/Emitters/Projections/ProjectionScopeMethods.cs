// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionScopeMethods(AxonEmissionContext context, ViewJavaTypes types, ProjectionScopeNode root, SemanticId owner)
{
    readonly ProjectionValues _values = new(context, types);
    readonly ProjectionRecords _records = new(context.Request.Model.Application, types);
    readonly ProjectionScopeMappings _mappings = new(context, types, owner);

    internal string Method(ProjectionScopeNode node, SemanticEventContract @event) => "apply" + node.Name + JavaNames.Type(@event.Name);

    internal void Emit(JavaCodeBuilder code, ProjectionScopeNode node, SemanticEventContract @event)
    {
        code.Block($"private Change<{node.Type}> {Method(node, @event)}({types.EventType(@event.Id)} event, EventMessage message, Object key, Object parentKey, {node.Type} current)", body =>
        {
            var creates = Creation(node, @event);
            body.Line($"if (current == null && !({creates})) return new Change<>(null, false, false);")
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
                body.Line($"if ({Matches(removal.Key, removal.ParentKey, @event, node)}) return new Change<>(null, true, true);");
            if (node.Identity is not null)
            {
                foreach (var removal in node.Scope.JoinRemovals.Where(removal => removal.EventContract == @event.Id))
                    body.Line($"if (java.util.Objects.equals(key, {_values.Key(removal.Key, @event)})) return new Change<>(null, true, false);");
            }
            Descendants(body, node, @event);
            if (node == root && node.Scope.Every is { SubscribesToAllEvents: true } && !Known(@event))
            {
                _mappings.Apply(body, node, node.Scope.Every.Mappings, @event);
                body.Line("touched = true;");
            }
            body.Line("return new Change<>(state, touched, ancestors);");
        });
    }

    internal void Caches(JavaCodeBuilder code, ProjectionScopeNode node, SemanticEventContract @event)
    {
        for (var index = 0; index < node.Scope.Joins.Length; index++)
        {
            var join = node.Scope.Joins[index];
            if (join.EventContract != @event.Id) continue;
            var key = _values.Key(join.Key ?? SemanticProjectionKey.EventSourceIdentity, @event);
            code.Line($"{ScopedProjectionEmitter.Cache(node, index)}.put(String.valueOf({key}), initial -> {{")
                .Line("var state = initial;");
            _mappings.Apply(code, node, join.Mappings, @event);
            code.Line("return state;").Line("});");
        }
    }

    void Joins(JavaCodeBuilder code, ProjectionScopeNode node, SemanticEventContract @event)
    {
        foreach (var join in node.Scope.Joins.Where(join => join.EventContract == @event.Id))
        {
            var property = node.Properties.First(property => property.Id == (node.Identity ?? join.On));
            var on = types.Unwrap(property.Type, $"state.{JavaNames.Member(property.Name)}()");
            var key = _values.Key(join.Key ?? SemanticProjectionKey.EventSourceIdentity, @event);
            code.Block($"if (current != null && java.util.Objects.equals(String.valueOf({on}), String.valueOf({key})))", selected =>
            {
                _mappings.Apply(selected, node, join.Mappings, @event);
                _mappings.Apply(selected, node, node.Scope.Every?.Mappings ?? [], @event);
                selected.Line("touched = true;");
            });
        }
    }

    void Hydrate(JavaCodeBuilder code, ProjectionScopeNode node)
    {
        for (var index = 0; index < node.Scope.Joins.Length; index++)
        {
            var join = node.Scope.Joins[index];
            var property = node.Properties.First(property => property.Id == (node.Identity ?? join.On));
            var value = $"state.{JavaNames.Member(property.Name)}()";
            var key = types.Unwrap(property.Type, value);
            code.Block($"if ({value} != null)", hydrate => hydrate
                .Line($"var joined = {ScopedProjectionEmitter.Cache(node, index)}.get(String.valueOf({key}));")
                .Line("if (joined != null) state = joined.apply(state);"));
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
        code.Line($"var {variable} = new java.util.ArrayList<{child.Type}>();")
            .Block($"if (state.{member}() != null)", items => items.Block($"for (var item : state.{member}())", item => item
                .Line($"var identity = String.valueOf({types.Unwrap(identity.Type, $"item.{JavaNames.Member(identity.Name)}()")});")
                .Line($"var changed = {Method(child, @event)}(event, message, identity, key, item);")
                .Line($"if (changed.value() != null) {variable}.add(changed.value());")
                .Line("childTouched |= changed.touched();").Line("childAncestors |= changed.ancestors();")));
        foreach (var from in child.Scope.From.Where(from => from.EventContract == @event.Id))
        {
            var key = _values.Key(from.Key, @event);
            var parentKey = _values.Key(from.ParentKey ?? SemanticProjectionKey.EventSourceIdentity, @event);
            if (parent.Scope.From.Any(candidate => candidate.EventContract == @event.Id)) parentKey = "key";
            code.Block($"if (java.util.Objects.equals(key, {parentKey}) && {variable}.stream().noneMatch(item -> java.util.Objects.equals(String.valueOf({types.Unwrap(identity.Type, $"item.{JavaNames.Member(identity.Name)}()")}), String.valueOf({key}))))", create => create
                .Line($"var added = {Method(child, @event)}(event, message, {key}, key, null);")
                .Line($"if (added.value() != null) {variable}.add(added.value());")
                .Line("childTouched |= added.touched();").Line("childAncestors |= added.ancestors();"));
        }
        code.Line($"state = {_records.Set(parent.Type, parent.Properties, [property.Id], "state", "java.util.List.copyOf(" + variable + ")")};");
    }

    string Matches(SemanticProjectionKey key, SemanticProjectionKey? parentKey, SemanticEventContract @event, ProjectionScopeNode node)
    {
        var result = $"java.util.Objects.equals(key, {_values.Key(key, @event) ?? "null"})";
        if (node.Identity is not null)
            result += $" && java.util.Objects.equals(parentKey, {_values.Key(parentKey ?? SemanticProjectionKey.EventSourceIdentity, @event) ?? "null"})";
        return result;
    }

    string Creation(ProjectionScopeNode node, SemanticEventContract @event)
    {
        var conditions = node.Scope.From.Where(from => from.EventContract == @event.Id).Select(from => Matches(from.Key, from.ParentKey, @event, node))
            .Concat(node.Nested.Select(nested => Creation(nested, @event))).ToList();
        if (node == root && node.Scope.Every is { SubscribesToAllEvents: true } && !Known(@event)) conditions.Add("true");
        return string.Join(" || ", conditions.DefaultIfEmpty("false"));
    }

    bool Known(SemanticEventContract @event) => root.All.Any(node => node.Scope.From.Any(from => from.EventContract == @event.Id) || node.Scope.Joins.Any(join => join.EventContract == @event.Id) || node.Scope.Removals.Any(removal => removal.EventContract == @event.Id) || node.Scope.JoinRemovals.Any(removal => removal.EventContract == @event.Id));
}
