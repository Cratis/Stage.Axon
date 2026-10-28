// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class RootProjectionEmitter(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
{
    readonly ProjectionScopeNode _root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));

    internal void Emit()
    {
        var name = JavaNames.Type(projection.Name) + "Projector";
        var code = new JavaCodeBuilder(package).Declare(name).Declare(_root.Type);
        foreach (var property in model.Properties) types.ClaimProjectionType(property.Type, code);
        new ProjectionUpdates(context, types, model.Id).Claim(code, package);
        foreach (var from in ProjectionTransitions.From(projection)) types.EventType(from.EventContract, code);
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name)})")
            .Line($"@{code.Reference("org.axonframework.messaging.core.annotation.Namespace")}({JavaCodeBuilder.StringLiteral(package)})")
            .Block($"public class {name}", body =>
            {
                Fields(body, name);
                foreach (var group in ProjectionTransitions.From(projection).GroupBy(from => from.EventContract))
                {
                    var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).First(candidate => candidate.Id == group.Key);
                    var values = new ProjectionValues(context, types, body);
                    var keys = group.Count() < 2 ? [] : ProjectionHandlerBinding.PreReserved([.. group.Select(from => values.Key(from.Key, @event))]);
                    new ProjectionHandlerBinding(context, types, model, package, keys: keys, sequence: new ProjectionSequencing(context, types, model).Projection(group, body)).Emit(
                    body,
                    types.EventType(group.Key, body),
                    method =>
                    {
                        foreach (var from in group) From(method, @event, from);
                    },
                    message: true);
                }
            });
        ViewJavaTypes.Add(context, package, name, code.Build(), projection.Id);
    }

    void Fields(JavaCodeBuilder code, string name)
    {
        var store = code.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"));
        var key = code.Reference("java.lang.Object");
        var clock = code.Reference("java.time.Clock");
        code.Line($"private final {store}<{key}, {_root.Type}> store;")
            .Line($"private final {clock} clock;")
            .Line($"@{code.Reference("org.springframework.beans.factory.annotation.Autowired")}")
            .Block($"public {name}(@{code.Reference("org.springframework.beans.factory.annotation.Qualifier")}({JavaCodeBuilder.StringLiteral(package + "." + _root.Type)}) {store}<{key}, {_root.Type}> store, {clock} clock)", constructor => constructor.Line("this.store = store;").Line("this.clock = clock;"))
            .Block($"public {name}({store}<{key}, {_root.Type}> store)", constructor => constructor.Line($"this(store, {clock}.systemUTC());"));
    }

    void From(JavaCodeBuilder code, SemanticEventContract @event, SemanticProjectionFrom from)
    {
        var values = new ProjectionValues(context, types, code);
        var records = new ProjectionRecords(context.Request.Model.Application, types, code);
        var mappings = new ProjectionScopeMappings(context, types, projection.Id, code);
        code.Line($"store.update({values.Key(from.Key, @event)}, current -> {{")
            .Line($"    var state = current == null ? {records.Empty(_root.Type, model.Properties)} : current;");
        mappings.Identify(code, _root, from, @event);
        mappings.Apply(code, _root, from.Mappings, @event);
        code.Line("    return state;").Line("});");
    }
}
