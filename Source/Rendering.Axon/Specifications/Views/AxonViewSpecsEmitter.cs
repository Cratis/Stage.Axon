// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class AxonViewSpecsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var types = new ViewJavaTypes(context.Request.Model.Application, context.BasePackage);
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
            new NativeQuerySpecifications(context, types, located).Emit();
        }
    }

    static void EmitModel(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model, SemanticSpecification[] specs)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(model.Name) + "ViewSpecificationsTest";
        var projection = located.Slice.Projections.FirstOrDefault(candidate => candidate.ReadModel == model.Id);
        if (projection is null) return;
        var http = new AxonViewHttpSpecs(context, types, located, model);
        var arguments = ProjectionHandlerShape.Arguments(projection, new(context, types, model.Id));
        var code = new JavaCodeBuilder(package).Declare(name);
        DeclareModels(code, located);
        ViewRepositories.Declare(code, model);
        var joined = ProjectionJoinEntities.Has(context, types, projection, model);
        if (joined) ViewRepositories.DeclareJoins(code, projection);
        code.Declare(JavaNames.Type(projection.Name) + "Projector");
        var plans = specs.Select(spec =>
        {
            var native = new NativeViewSpecifications(context, types, located, model).Plan(spec);
            return (Spec: spec, Native: native, Supported: (ViewSpecificationSupport.Legacy(spec, model, types) || native is not null) && ProjectionHandlerShape.Renderable(context, types, projection, model));
        }).ToArray();
        var claims = new ViewSpecificationClaims(context, types);
        foreach (var plan in plans.Where(plan => plan.Supported)) claims.Specification(plan.Spec, model, code);
        ViewRepositories.Annotate(code);
        code.Block($"class {name}", body =>
        {
            ViewRepositories.Field(body, model);
            if (joined) ViewRepositories.JoinField(body, projection);
            foreach (var (spec, native, supported) in plans)
            {
                var disabledCode = supported ? null : AxonRefusalCodes.ViewSpecification;
                if (!supported)
                {
                    context.Degradation.Unsupported(AxonRefusalCodes.ViewSpecification, spec.Id, "Execute the view specification's seeded state, action or exact/query expectations.", "execute the complete view specification", spec.Id.ToString());
                    body.Line($"@{body.Reference("org.junit.jupiter.api.Disabled")}({JavaCodeBuilder.StringLiteral(AxonRefusalCodes.ViewSpecification)})");
                }
                AxonSpecificationsEmitter.Register(context, spec.Id, package + "." + name, JavaNames.Member(spec.Name), disabledCode);
                http.Register(projection, spec);
                body.Line($"@{body.Reference("org.junit.jupiter.api.Test")}").Line($"@{body.Reference("org.junit.jupiter.api.DisplayName")}({JavaCodeBuilder.StringLiteral(spec.Id.ToString())})").Block($"void {JavaNames.Member(spec.Name)}()", NativeViewSpecifications.Dispatch(native, method =>
                {
                    if (!supported)
                    {
                        method.UnsupportedIntent("execute the complete view specification", spec.Id.ToString(), AxonRefusalCodes.ViewSpecification)
                            .Line("throw new UnsupportedOperationException(\"Unsupported modeled view specification\");");
                        return;
                    }
                    method.Line($"var projector = new {JavaNames.Type(projection.Name)}Projector(repository{(joined ? ", joins" : string.Empty)});");
                    foreach (var given in spec.GivenEvents)
                    {
                        var root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));
                        if (!root.All.Any(node => node.Scope.From.Any(from => from.EventContract == given.EventContract) || node.Scope.Joins.Any(join => join.EventContract == given.EventContract) || node.Scope.Removals.Any(removal => removal.EventContract == given.EventContract) || node.Scope.JoinRemovals.Any(removal => removal.EventContract == given.EventContract) || node.Scope.Every is { SubscribesToAllEvents: true })) continue;
                        method.Line($"projector.on({AxonViewEvents.Expression(context, types, given, method)}{arguments});");
                    }
                    foreach (var expected in spec.ThenReadModels.Where(expected => expected.ReadModel == model.Id))
                        EmitExpected(context.Request.Model.Application, types, model, expected, method);
                    foreach (var absent in spec.ThenAbsentReadModels.Where(expected => expected.ReadModel == model.Id))
                    {
                        var key = ViewExpectedKeys.Expression(absent.Key, model, types, method);
                        var missing = Missing(model, types, key);
                        method.Line($"{method.Reference("org.junit.jupiter.api.Assertions")}.assertTrue({missing}, \"Expected the modeled instance to be removed\");");
                    }
                }));
            }
        });
        context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}.java", code.Build(), [.. specs.Select(specification => specification.Id)]));
    }

    static void DeclareModels(JavaCodeBuilder code, LocatedSemanticSlice located)
    {
        foreach (var model in located.Slice.ReadModels) code.Declare(JavaNames.Type(model.Name));
    }

    static string Missing(SemanticReadModel model, ViewJavaTypes types, string? key) =>
        types.KeyParts(model) is not null ? $"!repository.existsById({key})" : $"{ViewRepositories.Get(model, key)}.findAny().isEmpty()";

    static void EmitExpected(SemanticApplication application, ViewJavaTypes types, SemanticReadModel model, SemanticSpecificationReadModel expected, JavaCodeBuilder code)
    {
        var matcher = new ViewExpectations(application, types, code);
        var predicates = expected.Values.Select(entry =>
        {
            var property = model.Properties.First(candidate => candidate.Id == entry.TargetProperty);
            var actual = $"row.{JavaNames.Member(property.Name)}()";
            return matcher.Predicate(entry.Value, property.Type, actual);
        });
        var key = ViewExpectedKeys.Expression(expected.Key, model, types, code);
        var rows = key is null ? ViewRepositories.All(model) : ViewRepositories.Get(model, key);
        code.Line($"{code.Reference("org.junit.jupiter.api.Assertions")}.assertTrue({rows}.anyMatch(row -> {string.Join(" && ", predicates.DefaultIfEmpty("true"))}), \"Expected modeled read-model properties\");");
    }
}
