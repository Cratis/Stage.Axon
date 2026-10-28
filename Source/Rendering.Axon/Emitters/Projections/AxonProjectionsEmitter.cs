// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class AxonProjectionsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var types = new ViewJavaTypes(context.Request.Model.Application, context.BasePackage);
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
            namespaces.UnionWith(EmitReducers(context, types, located));
        }
        new AxonViewSpecsEmitter().Emit(context);
        if (namespaces.Count > 0)
        {
            context.AddJava(types.BasePackage + ".infrastructure", "DefaultViewProcessorGuard", PersistentJavaTemplate.Render(DefaultViewProcessorGuard.Source
                .Replace("__PACKAGE__", types.BasePackage + ".infrastructure", StringComparison.Ordinal)
                .Replace("__NAMESPACES__", string.Join(", ", namespaces.Select(JavaCodeBuilder.StringLiteral)), StringComparison.Ordinal)));
        }
        if (ScopeSelection.Select(context.Request).Any(owner => !owner.Slice.ReadModels.IsEmpty))
            context.Artifacts.Add(PlannedArtifact.CreateText("src/main/resources/axon-views.properties", string.Join('\n', namespaces.Select(name => $"axon.eventhandling.processors[{name}].mode=${{stage.axon.view-processor-mode:subscribing}}")) + "\n", []));
    }

    static IEnumerable<string> EmitReducers(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located)
    {
        foreach (var reducer in located.Slice.Reducers)
        {
            var model = located.Slice.ReadModels.FirstOrDefault(candidate => candidate.Id == reducer.ReadModel);
            var admission = ReducerBodies.Admission(context, located, reducer);
            if (model is null || admission.Blocked is not null)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.Reduction, model?.Id ?? located.Slice.Id, admission.Blocked ?? "The reducer read model is outside its slice.");
                continue;
            }
            new ReducerProjection(context, types).Emit(located, model, reducer, admission);
            yield return types.Package(located);
        }
    }

    static void EmitModel(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticReadModel model)
    {
        var name = JavaNames.Type(model.Name);
        ViewJavaTypes.Add(context, package, name, ReadModelRecords.Code(types, package, model), model.Id);
        var reason = PersistentViewAdmission.RefusalReason(context, model.Id);
        if (reason is null)
        {
            context.Degradation.Unsupported(AxonRefusalCodes.ReadModelPersistence, model.Id, "Default-profile read-model rows are volatile; durable checkpoints may skip prior events after restart. The persistent profile stores this read model durably, with generation checks and processor-bound transactions.");
            PersistentProjectionStore.Emit(context, types, package, model);
            return;
        }
        context.Degradation.Unsupported(AxonRefusalCodes.ReadModelPersistence, model.Id, $"Default-profile read-model rows and projector indexes are volatile; durable checkpoints may skip prior events after restart. Persistent startup refuses this read model because {reason}; automatic replay is not established recovery.");
        var configuration = new JavaCodeBuilder(package).Declare(name).Declare(name + "StoreConfiguration");
        var storeType = configuration.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"));
        var memoryStoreType = configuration.Reference(AxonRuntimePackage.Type(types.BasePackage, "InMemoryReadModelStore"));
        var safetyType = configuration.Reference(AxonRuntimePackage.Type(types.BasePackage, "persistent.PersistentReadModelSafety"));
        configuration
            .DiffersFromScreenplay($"Default-profile read-model rows and projector indexes are volatile; durable checkpoints may skip prior events after restart. Persistent startup refuses this read model because {reason}; automatic replay is not established recovery ({AxonRefusalCodes.ReadModelPersistence}).")
            .Line($"@{configuration.Reference("org.springframework.context.annotation.Configuration")}({JavaCodeBuilder.StringLiteral(package + "." + name + "StoreConfiguration")})")
            .Line($"@{configuration.Reference("org.springframework.context.annotation.PropertySource")}(\"classpath:axon-views.properties\")")
            .Block($"public class {name}StoreConfiguration", body => body
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({JavaCodeBuilder.StringLiteral(package + "." + name)})")
                .Block($"public {storeType}<{body.Reference("java.lang.Object")}, {name}> store()", method => method.Line($"return new {memoryStoreType}<>();"))
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({JavaCodeBuilder.StringLiteral(package + "." + name + ".persistentReadModelSafety")})")
                .Line($"@{body.Reference("org.springframework.context.annotation.Profile")}(\"persistent\")")
                .Block($"public static {safetyType} persistentReadModelSafety()", method =>
                    method.Line($"return new {safetyType}({JavaCodeBuilder.StringLiteral(package + "." + name)});")));
        ViewJavaTypes.Add(context, package, name + "StoreConfiguration", configuration.Build(), model.Id);
    }

    static void EmitProjector(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
    {
        if (ProjectionHandlerShape.Scoped(projection))
        {
            new ScopedProjectionEmitter(context, types, package, projection, model).Emit();
            return;
        }
        var modelName = JavaNames.Type(model.Name);
        var name = JavaNames.Type(projection.Name) + "Projector";
        var updates = new ProjectionUpdates(context, types, model.Id);
        var code = new JavaCodeBuilder(package).Declare(modelName).Declare(name);
        foreach (var property in model.Properties) types.ClaimProjectionType(property.Type, code);
        var storeType = code.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"));
        updates.Claim(code, package);
        foreach (var from in ProjectionTransitions.From(projection)) types.EventType(from.EventContract, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name)})")
            .Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(package)})")
            .Block($"public class {name}", body =>
        {
            body.Line($"private final {storeType}<{body.Reference("java.lang.Object")}, {modelName}> store;")
                .Block($"public {name}(@{body.Reference("org.springframework.beans.factory.annotation.Qualifier")}({JavaCodeBuilder.StringLiteral(package + "." + modelName)}) {storeType}<{body.Reference("java.lang.Object")}, {modelName}> store)", constructor => constructor.Line("this.store = store;"));
            foreach (var group in ProjectionTransitions.From(projection).GroupBy(from => from.EventContract))
            {
                var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(candidate => candidate.Id == group.Key);
                var values = new ProjectionValues(context, types, body);
                var keys = group.Count() < 2 ? [] : ProjectionHandlerBinding.PreReserved([.. group.Select(from => values.Key(from.Key, @event))]);
                new ProjectionHandlerBinding(context, types, model, package, keys: keys).Emit(body, types.EventType(group.Key, body), method =>
                {
                    foreach (var from in group) EmitFrom(context, types, method, projection, model, @event, from);
                });
            }
        });
        ViewJavaTypes.Add(context, package, name, code.Build(), projection.Id);
    }

    static void EmitFrom(AxonEmissionContext context, ViewJavaTypes types, JavaCodeBuilder code, SemanticProjection projection, SemanticReadModel model, SemanticEventContract @event, SemanticProjectionFrom from)
    {
        var values = new ProjectionValues(context, types, code);
        var key = values.Key(from.Key, @event);
        if (key is null)
        {
            context.Degradation.Unsupported(AxonRefusalCodes.CompositeKey, projection.Id, "This projection key cannot yet be emitted as a Java key.");
            code.UnsupportedIntent("identify the projected instance", projection.Id.ToString(), AxonRefusalCodes.CompositeKey);
            return;
        }
        var modelName = JavaNames.Type(model.Name);
        code.Line($"store.update({key}, current -> {{")
            .Line($"    var state = current == null ? new {modelName}({string.Join(", ", model.Properties.Select(property => types.Default(property.Type, code)))}) : current;");
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
            code.Line($"    state = state.with{JavaNames.Type(target!.Name)}({expression});");
        }
        code.Line("    return state;").Line("});");
    }
}
