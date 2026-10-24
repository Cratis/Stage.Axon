// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class AxonViewSpecsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var types = new ViewJavaTypes(context.Request.Model.Application);
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            foreach (var model in located.Slice.ReadModels)
            {
                var specs = located.Slice.Specifications.Where(specification => specification.ThenReadModels.Any(expected => expected.ReadModel == model.Id)).ToArray();
                if (specs.Length == 0) continue;
                EmitModel(context, types, located, model, specs);
            }
        }
    }

    static void EmitModel(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model, SemanticSpecification[] specs)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(model.Name) + "ViewSpecificationsTest";
        var projection = located.Slice.Projections.FirstOrDefault(candidate => candidate.ReadModel == model.Id);
        if (projection is null) return;
        var code = new JavaCodeBuilder(package).Import("org.junit.jupiter.api.Test")
            .Import(types.BasePackage + ".runtime.ReadModelStore").Import(types.BasePackage + ".runtime.InMemoryReadModelStore")
            .Import(types.BasePackage + ".runtime.EventSourceId");
        code.Block($"class {name}", body =>
        {
            foreach (var spec in specs)
            {
                AxonSpecificationsEmitter.Register(context, spec.Id, package + "." + name, JavaNames.Member(spec.Name));
                body.Line("@Test").Line($"@org.junit.jupiter.api.DisplayName({JavaCodeBuilder.StringLiteral(spec.Id.ToString())})").Block($"void {JavaNames.Member(spec.Name)}()", method =>
                {
                    method.Line($"ReadModelStore<Object, {JavaNames.Type(model.Name)}> store = new InMemoryReadModelStore<>();")
                        .Line($"var projector = new {JavaNames.Type(projection.Name)}Projector(store);");
                    foreach (var given in spec.GivenEvents)
                    {
                        var root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));
                        if (!root.All.Any(node => node.Scope.From.Any(from => from.EventContract == given.EventContract) || node.Scope.Joins.Any(join => join.EventContract == given.EventContract) || node.Scope.Removals.Any(removal => removal.EventContract == given.EventContract) || node.Scope.JoinRemovals.Any(removal => removal.EventContract == given.EventContract) || node.Scope.Every is { SubscribesToAllEvents: true })) continue;
                        EmitEvent(context, types, given, method);
                    }
                    foreach (var expected in spec.ThenReadModels.Where(expected => expected.ReadModel == model.Id))
                        EmitExpected(types, model, expected, method);
                });
            }
        });
        context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}.java", code.Build(), [.. specs.Select(specification => specification.Id)]));
    }

    static void EmitEvent(AxonEmissionContext context, ViewJavaTypes types, SemanticSpecificationEvent given, JavaCodeBuilder code)
    {
        var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(candidate => candidate.Id == given.EventContract);
        var source = given.EventSource?.Value is SemanticTextValue text ? JavaCodeBuilder.StringLiteral(text.Value) : "\"spec-source\"";
        var primary = AxonEventSources.PrimaryTag(context, @event);
        var arguments = @event.Properties.Select(property => given.Values.FirstOrDefault(value => value.TargetProperty == property.Id) is { } value ? types.Literal(value.Value, property.Type) : types.Default(property.Type)).ToList();
        if (!@event.Properties.Any(property => JavaNames.Member(property.Name) == primary.ComponentName))
        {
            var identity = $"new {primary.JavaType}({source})";
            if (primary.JavaType != types.BasePackage + ".runtime.EventSourceId" && given.EventSource is { } eventSource)
                identity = types.Literal(eventSource.Value, eventSource.Type);
            arguments.Insert(0, identity);
        }
        code.Line($"projector.on(new {types.EventType(given.EventContract)}({string.Join(", ", arguments)}));");
    }

    static void EmitExpected(ViewJavaTypes types, SemanticReadModel model, SemanticSpecificationReadModel expected, JavaCodeBuilder code)
    {
        var predicates = expected.Values.Select(entry =>
        {
            var property = model.Properties.First(candidate => candidate.Id == entry.TargetProperty);
            var actual = $"row.{JavaNames.Member(property.Name)}()";
            var value = types.Literal(entry.Value, property.Type);
            if (types.PrimitiveOf(property.Type) == SemanticPrimitiveType.DecimalNumber && entry.Value is not SemanticNullValue)
                return $"({actual} != null && {types.Unwrap(property.Type, actual)}.compareTo({types.Unwrap(property.Type, value)}) == 0)";
            return $"java.util.Objects.equals({actual}, {value})";
        });
        code.Line($"org.junit.jupiter.api.Assertions.assertTrue(store.all().stream().anyMatch(row -> {string.Join(" && ", predicates.DefaultIfEmpty("true"))}), \"Expected modeled read-model properties\");");
    }
}
