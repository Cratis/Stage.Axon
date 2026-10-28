// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class RootProjectionEmitter(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
{
    internal void Emit()
    {
        var name = JavaNames.Type(projection.Name) + "Projector";
        var entity = ReadModelEntities.EntityName(model);
        var modelName = JavaNames.Type(model.Name);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(modelName).Declare(entity).Declare(ReadModelEntities.RepositoryName(model));
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
                    new ProjectionHandlerBinding(context, types, model, package).Emit(
                        body,
                        types.EventType(group.Key, body),
                        method =>
                        {
                            foreach (var from in group) From(method, @event, from);
                        },
                        message: ProjectionHandlerShape.Scoped(projection));
                }
            });
        ViewJavaTypes.Add(context, package, name, code.Build(), projection.Id);
    }

    void Fields(JavaCodeBuilder code, string name)
    {
        var repository = ReadModelEntities.RepositoryName(model);
        var clock = code.Reference("java.time.Clock");
        code.Line($"private final {repository} repository;")
            .Line($"private final {clock} clock;")
            .Line($"@{code.Reference("org.springframework.beans.factory.annotation.Autowired")}")
            .Block($"public {name}({repository} repository, {clock} clock)", constructor => constructor.Line("this.repository = repository;").Line("this.clock = clock;"))
            .Block($"public {name}({repository} repository)", constructor => constructor.Line($"this(repository, {clock}.systemUTC());"));
    }

    void From(JavaCodeBuilder code, SemanticEventContract @event, SemanticProjectionFrom from)
    {
        var values = new ProjectionValues(context, types, code);
        var key = values.Key(from.Key, @event);
        if (key is null)
        {
            context.Degradation.Unsupported(AxonRefusalCodes.CompositeKey, projection.Id, "This projection key cannot yet be emitted as a Java key.");
            code.UnsupportedIntent("identify the projected instance", projection.Id.ToString(), AxonRefusalCodes.CompositeKey);
            return;
        }

        var records = new ProjectionRecords(context.Request.Model.Application, types, code);
        var modelName = JavaNames.Type(model.Name);
        var entity = ReadModelEntities.EntityName(model);

        // The row is read first and each mapping is applied to it, so no concept is ever constructed from a placeholder seed.
        code.Line("{")
            .Line($"    var key = {key};")
            .Line($"    var state = repository.findById(key).map({entity}::toModel).orElseGet(() -> {records.Empty(modelName, model.Properties)});");
        foreach (var mapping in from.Mappings)
        {
            var target = mapping.Target.Length == 0 ? null : records.Property(model.Properties, mapping.Target);
            var expression = target is null ? null : values.Mapping(mapping, target, @event, records.Access(model.Properties, mapping.Target, "state"));
            if (expression is null)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.ProjectionMapping, projection.Id, "The projection mapping is not available in the Axon target.");
                code.UnsupportedIntent("map the projected property", projection.Id.ToString(), AxonRefusalCodes.ProjectionMapping);
                continue;
            }
            code.Line($"    state = {records.Set(modelName, model.Properties, mapping.Target, "state", expression)};");
        }
        code.Line($"    repository.save(new {entity}(key, state));").Line("}");
    }
}
