// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class NativeViewSpecifications(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model)
{
    internal static Action<JavaCodeBuilder> Dispatch(Action<JavaCodeBuilder>? native, Action<JavaCodeBuilder> legacy) => native ?? legacy;

    internal Action<JavaCodeBuilder>? Plan(SemanticSpecification spec)
    {
        var projection = located.Slice.Projections.FirstOrDefault(candidate => candidate.ReadModel == model.Id);
        if (projection is null || ProjectionHandlerShape.Scoped(projection)) return null;
        if (!Supported(spec)) return null;
        var root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name));
        if (!Handles(root, spec.GivenEvents[0])) return null;
        var given = AxonViewEvents.Normalize(context, spec.GivenEvents[0]);
        if (given is null) return null;
        var appended = Appended(root, spec.WhenAppended);
        if (spec.WhenAppended is not null && appended is null) return null;
        var body = new Body(JavaNames.Type(model.Name), JavaNames.Type(projection.Name), given, appended, SourceKey(root, given), ProjectionHandlerShape.Arguments(projection, new(context, types, model.Id)));
        return method => Emit(method, spec, body);
    }

    internal string Assertion(SemanticSpecificationReadModel expected, string sourceKey, JavaCodeBuilder code)
    {
        var matcher = new ViewExpectations(context.Request.Model.Application, types, code);
        var predicates = expected.Values.Select(entry =>
        {
            var property = model.Properties.First(candidate => candidate.Id == entry.TargetProperty);
            return matcher.Predicate(entry.Value, property.Type, $"row.{JavaNames.Member(property.Name)}()");
        });
        var key = ViewExpectedKeys.Expression(expected.Key, model, types, code) ?? sourceKey;
        return $"{code.Reference("org.junit.jupiter.api.Assertions")}.assertTrue(store.get({key}).stream().anyMatch(row -> {string.Join(" && ", predicates)}), \"Expected modeled read-model properties\");";
    }

    static bool Handles(ProjectionScopeNode root, SemanticSpecificationEvent given) =>
        root.Scope.From.Any(from => from.EventContract == given.EventContract && from.Key is SemanticProjectionValueKey { Value: SemanticProjectionEventSourceIdentity or SemanticProjectionLiteral { Value: SemanticTextValue } });

    static string SourceKey(ProjectionScopeNode root, SemanticSpecificationEvent given)
    {
        var key = root.Scope.From.First(from => from.EventContract == given.EventContract).Key;
        return JavaCodeBuilder.StringLiteral(key is SemanticProjectionValueKey { Value: SemanticProjectionLiteral { Value: SemanticTextValue text } } ? text.Value : ((SemanticTextValue)given.EventSource!.Value).Value);
    }

    static bool PureGiven(SemanticSpecification spec) =>
        spec.GivenCaller is { Authenticated: true, Claims.Length: 0 } && spec.GivenClock is null && spec.GivenReadModels.IsEmpty && spec.GivenEvents.Length == 1;

    static bool NoWhen(SemanticSpecification spec) =>
        spec.When is null && spec.WhenRedelivered is null && spec.WhenClock is null && spec.WhenCapture is null && spec.WhenTrigger is null;

    static bool PureThen(SemanticSpecification spec) =>
        !spec.ThenDenied && spec.ThenErrors.IsEmpty && spec.ThenEvents.IsEmpty && spec.ThenQueries.IsEmpty && spec.ThenAbsentReadModels.IsEmpty && spec.ThenReadModels.Length == 1;

    SemanticSpecificationEvent? Appended(ProjectionScopeNode root, SemanticSpecificationAppend? append)
    {
        if (append is null) return null;
        var given = new SemanticSpecificationEvent(append.EventContract, append.Values) { EventSource = append.EventSource, Route = append.Route };
        return Handles(root, given) ? AxonViewEvents.Normalize(context, given) : null;
    }

    void Emit(JavaCodeBuilder method, SemanticSpecification spec, Body plan)
    {
        method.Block("try", body =>
        {
            CommandFixtureSecurity.CallerReferences(body, spec.GivenCaller);
            body.Line($"{body.Reference(AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"))}<Object, {plan.Model}> store = new {body.Reference(AxonRuntimePackage.Type(types.BasePackage, "InMemoryReadModelStore"))}<>();")
                .Line($"var projector = new {plan.Projection}Projector(store);")
                .Line($"projector.on({AxonViewEvents.Expression(context, types, plan.Event, body)}{plan.Arguments});");
            if (plan.Appended is { } appended) body.Line($"projector.on({AxonViewEvents.Expression(context, types, appended, body)}{plan.Arguments});");
            foreach (var expected in spec.ThenReadModels) body.Line(Assertion(expected, plan.SourceKey, body));
        }).Block("finally", body => body.Line($"{body.Reference("org.springframework.security.core.context.SecurityContextHolder")}.clearContext();"));
    }

    bool Supported(SemanticSpecification spec) =>
        PureGiven(spec) && NoWhen(spec) && PureThen(spec) && spec.ThenReadModels.All(ExpectedSupported);

    bool ExpectedSupported(SemanticSpecificationReadModel expected) =>
        expected.ReadModel == model.Id && !expected.Exactly && expected.Values.Length > 0 &&
        expected.Values.All(entry => model.Properties.Any(property => property.Id == entry.TargetProperty));

    sealed record Body(string Model, string Projection, SemanticSpecificationEvent Event, SemanticSpecificationEvent? Appended, string SourceKey, string Arguments);
}
