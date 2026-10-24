// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
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
                var specs = located.Slice.Specifications.Where(specification => !context.Specifications.ContainsKey(specification.Id) && (specification.ThenReadModels.Any(expected => expected.ReadModel == model.Id) || specification.ThenAbsentReadModels.Any(expected => expected.ReadModel == model.Id))).ToArray();
                if (specs.Length == 0) continue;
                if (located.Slice.Reducers.Any(reducer => reducer.ReadModel == model.Id))
                {
                    ReducerViewSpecifications.Emit(context, types, located, model, specs);
                    continue;
                }

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
        var http = new AxonViewHttpSpecs(context, types, located, model);
        var code = new JavaCodeBuilder(package).Import("org.junit.jupiter.api.Test")
            .Import(types.BasePackage + ".runtime.ReadModelStore").Import(types.BasePackage + ".runtime.InMemoryReadModelStore")
            .Import(types.BasePackage + ".runtime.EventSourceId");
        code.Block($"class {name}", body =>
        {
            foreach (var spec in specs)
            {
                var supported = spec.GivenReadModels.Length == 0 && spec.When is null && spec.WhenAppended is null && spec.WhenRedelivered is null && spec.WhenClock is null && spec.WhenCapture is null && spec.WhenTrigger is null && spec.GivenClock is null && spec.GivenCaller is null && !spec.ThenDenied && spec.ThenErrors.Length == 0 && spec.ThenEvents.Length == 0 && spec.ThenQueries.Length == 0 && spec.ThenReadModels.All(expected => !expected.Exactly) && spec.ThenReadModels.Select(expected => expected.ReadModel).Concat(spec.ThenAbsentReadModels.Select(expected => expected.ReadModel)).Distinct().Count() == 1 && spec.ThenAbsentReadModels.All(expected => ViewExpectedKeys.Expression(expected.Key, model, types) is not null);
                var disabledCode = supported ? null : AxonRefusalCodes.ViewSpecification;
                if (!supported)
                {
                    context.Degradation.Unsupported(AxonRefusalCodes.ViewSpecification, spec.Id, "Execute the view specification's seeded state, action or exact/query expectations.", "execute the complete view specification", spec.Id.ToString());
                    body.Line($"@org.junit.jupiter.api.Disabled({JavaCodeBuilder.StringLiteral(AxonRefusalCodes.ViewSpecification)})");
                }
                AxonSpecificationsEmitter.Register(context, spec.Id, package + "." + name, JavaNames.Member(spec.Name), disabledCode);
                http.Register(projection, spec);
                body.Line("@Test").Line($"@org.junit.jupiter.api.DisplayName({JavaCodeBuilder.StringLiteral(spec.Id.ToString())})").Block($"void {JavaNames.Member(spec.Name)}()", method =>
                {
                    if (!supported)
                    {
                        method.UnsupportedIntent("execute the complete view specification", spec.Id.ToString(), AxonRefusalCodes.ViewSpecification)
                            .Line("throw new UnsupportedOperationException(\"Unsupported modeled view specification\");");
                        return;
                    }
                    method.Line($"ReadModelStore<Object, {JavaNames.Type(model.Name)}> store = new InMemoryReadModelStore<>();")
                        .Line($"var projector = new {JavaNames.Type(projection.Name)}Projector(store);");
                    foreach (var given in spec.GivenEvents)
                    {
                        var root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));
                        if (!root.All.Any(node => node.Scope.From.Any(from => from.EventContract == given.EventContract) || node.Scope.Joins.Any(join => join.EventContract == given.EventContract) || node.Scope.Removals.Any(removal => removal.EventContract == given.EventContract) || node.Scope.JoinRemovals.Any(removal => removal.EventContract == given.EventContract) || node.Scope.Every is { SubscribesToAllEvents: true })) continue;
                        method.Line($"projector.on({AxonViewEvents.Expression(context, types, given)});");
                    }
                    foreach (var expected in spec.ThenReadModels.Where(expected => expected.ReadModel == model.Id))
                        EmitExpected(context.Request.Model.Application, types, model, expected, method);
                    foreach (var absent in spec.ThenAbsentReadModels.Where(expected => expected.ReadModel == model.Id))
                        method.Line($"org.junit.jupiter.api.Assertions.assertTrue(store.get({ViewExpectedKeys.Expression(absent.Key, model, types)}).isEmpty(), \"Expected the modeled instance to be removed\");");
                });
            }
        });
        context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}.java", code.Build(), [.. specs.Select(specification => specification.Id)]));
    }

    static void EmitExpected(SemanticApplication application, ViewJavaTypes types, SemanticReadModel model, SemanticSpecificationReadModel expected, JavaCodeBuilder code)
    {
        var matcher = new ViewExpectations(application, types);
        var predicates = expected.Values.Select(entry =>
        {
            var property = model.Properties.First(candidate => candidate.Id == entry.TargetProperty);
            var actual = $"row.{JavaNames.Member(property.Name)}()";
            return matcher.Predicate(entry.Value, property.Type, actual);
        });
        var key = ViewExpectedKeys.Expression(expected.Key, model, types);
        var rows = key is null ? "store.all().stream()" : $"store.get({key}).stream()";
        code.Line($"org.junit.jupiter.api.Assertions.assertTrue({rows}.anyMatch(row -> {string.Join(" && ", predicates.DefaultIfEmpty("true"))}), \"Expected modeled read-model properties\");");
    }
}
