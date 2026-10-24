// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class AxonProjectionsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var types = new ViewJavaTypes(context.Request.Model.Application);
        var namespaces = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            var package = types.Package(located);
            foreach (var model in located.Slice.ReadModels) EmitModel(context, types, package, model);
            foreach (var projection in located.Slice.Projections)
            {
                var model = located.Slice.ReadModels.FirstOrDefault(candidate => candidate.Id == projection.ReadModel);
                if (model is null) continue;
                EmitProjector(context, types, package, projection, model);
                namespaces.Add(package);
            }
            foreach (var reducer in located.Slice.Reducers)
            {
                var model = located.Slice.ReadModels.First(candidate => candidate.Id == reducer.ReadModel);
                ReducerProjection.Emit(context, types, located, model, reducer);
                namespaces.Add(package);
            }
        }
        new AxonViewSpecsEmitter().Emit(context);
        if (namespaces.Count > 0)
            context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/axon-views.properties", string.Join('\n', namespaces.Select(name => $"axon.eventhandling.processors.{name}.mode=subscribing")) + "\n", []));
    }

    static void EmitModel(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticReadModel model)
    {
        var name = JavaNames.Type(model.Name);
        var components = string.Join(", ", model.Properties.Select(property => $"@com.fasterxml.jackson.annotation.JsonProperty({JavaCodeBuilder.StringLiteral(property.Name)}) {types.Type(property.Type)} {JavaNames.Member(property.Name)}"));
        ViewJavaTypes.Add(context, package, name, new JavaCodeBuilder(package).Line($"public record {name}({components}) {{}}").Build(), model.Id);
        var configuration = new JavaCodeBuilder(package)
            .Import("org.springframework.context.annotation.Configuration").Import("org.springframework.context.annotation.Bean")
            .Import("org.springframework.context.annotation.PropertySource")
            .Import(types.BasePackage + ".runtime.ReadModelStore").Import(types.BasePackage + ".runtime.InMemoryReadModelStore")
            .Line($"@Configuration({JavaCodeBuilder.StringLiteral(package + "." + name + "StoreConfiguration")})")
            .Line("@PropertySource(\"classpath:axon-views.properties\")")
            .Block($"public class {name}StoreConfiguration", body => body
                .Line($"@Bean({JavaCodeBuilder.StringLiteral(package + "." + name)})")
                .Block($"public ReadModelStore<Object, {name}> store()", method => method.Line("return new InMemoryReadModelStore<>();")));
        ViewJavaTypes.Add(context, package, name + "StoreConfiguration", configuration.Build(), model.Id);
    }

    static void EmitProjector(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
    {
        if (projection.Scope is { } scope && (scope.Every is not null || scope.Children.Length > 0 || scope.Nested.Length > 0 || scope.Joins.Length > 0 || scope.Removals.Length > 0 || scope.JoinRemovals.Length > 0 || scope.From.Any(from => from.Key is not SemanticProjectionValueKey || from.Mappings.Any(mapping => mapping.Target.Length > 1 || mapping.Source is SemanticProjectionEventContextValue))))
        {
            new ScopedProjectionEmitter(context, types, package, projection, model).Emit();
            return;
        }
        var modelName = JavaNames.Type(model.Name);
        var name = JavaNames.Type(projection.Name) + "Projector";
        var updates = new ProjectionUpdates(context, types, model.Id);
        var code = new JavaCodeBuilder(package).Import("org.springframework.stereotype.Component")
            .Import("org.springframework.beans.factory.annotation.Qualifier")
            .Import("org.axonframework.messaging.core.annotation.Namespace")
            .Import("org.axonframework.messaging.eventhandling.annotation.EventHandler")
            .Import(types.BasePackage + ".runtime.ReadModelStore")
            .Line($"@Component({JavaCodeBuilder.StringLiteral(package + "." + name)})")
            .Line($"@Namespace({JavaCodeBuilder.StringLiteral(package)})");
        code.Block($"public class {name}", body =>
        {
            body.Line($"private final ReadModelStore<Object, {modelName}> store;")
                .Block($"public {name}(@Qualifier({JavaCodeBuilder.StringLiteral(package + "." + modelName)}) ReadModelStore<Object, {modelName}> store)", constructor => constructor.Line("this.store = store;"));
            foreach (var group in ProjectionTransitions.From(projection).GroupBy(from => from.EventContract))
            {
                var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(candidate => candidate.Id == group.Key);
                if (updates.Enabled) body.Block($"public void on({types.EventType(group.Key)} event)", method => method.Line("on(event, null);"));
                body.Line("@EventHandler").Block($"public void on({types.EventType(group.Key)} event{updates.Parameter})", method =>
                {
                    foreach (var from in group) EmitFrom(context, types, method, projection, model, @event, from);
                    updates.Notify(method);
                });
            }
        });
        ViewJavaTypes.Add(context, package, name, code.Build(), projection.Id);
    }

    static void EmitFrom(AxonEmissionContext context, ViewJavaTypes types, JavaCodeBuilder code, SemanticProjection projection, SemanticReadModel model, SemanticEventContract @event, SemanticProjectionFrom from)
    {
        var values = new ProjectionValues(context, types);
        var key = values.Key(from.Key, @event);
        if (key is null)
        {
            context.Degradation.Unsupported(AxonRefusalCodes.CompositeKey, projection.Id, "This projection key cannot yet be emitted as a Java key.");
            code.UnsupportedIntent("identify the projected instance", projection.Id.ToString(), AxonRefusalCodes.CompositeKey);
            return;
        }
        var modelName = JavaNames.Type(model.Name);
        code.Line($"store.update({key}, current -> {{")
            .Line($"    var state = current == null ? new {modelName}({string.Join(", ", model.Properties.Select(property => types.Default(property.Type)))}) : current;");
        foreach (var mapping in from.Mappings)
        {
            var target = mapping.Target.Length == 1 ? model.Properties.FirstOrDefault(property => property.Id == mapping.Target[0]) : null;
            var expression = target is null ? null : values.Mapping(mapping, target, @event);
            if (expression is null)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.ProjectionMapping, projection.Id, "The projection mapping is not available in the Axon target.");
                code.UnsupportedIntent("map the projected property", projection.Id.ToString(), AxonRefusalCodes.ProjectionMapping);
                continue;
            }
            var arguments = model.Properties.Select(property => property.Id == target!.Id ? expression : $"state.{JavaNames.Member(property.Name)}()");
            code.Line($"    state = new {modelName}({string.Join(", ", arguments)});");
        }
        code.Line("    return state;").Line("});");
    }
}
