// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class NativeQuerySpecifications(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located)
{
    internal void Emit()
    {
        foreach (var spec in located.Slice.Specifications.Where(spec => !context.Specifications.ContainsKey(spec.Id) && Supported(spec)))
            if (Plan(spec) is { } plan) Emit(plan);
    }

    static bool Supported(SemanticSpecification spec) => FixturesSupported(spec) && NoAction(spec) && OnlyQueryResults(spec);

    static bool FixturesSupported(SemanticSpecification spec) =>
        spec.GivenCaller is null && spec.GivenClock is null && spec.GivenReadModels.IsEmpty && spec.GivenCaptures.IsEmpty && spec.GivenGenerated.IsEmpty && !spec.GivenEvents.IsEmpty;

    static bool NoAction(SemanticSpecification spec) =>
        spec.When is null && spec.WhenAppended is null && spec.WhenRedelivered is null && spec.WhenClock is null && spec.WhenCapture is null && spec.WhenTrigger is null;

    static bool OnlyQueryResults(SemanticSpecification spec) =>
        NoEventOutcome(spec) && spec.ThenReadModels.IsEmpty && spec.ThenAbsentReadModels.IsEmpty && spec.ThenReturns is null && spec.ThenQueries.Length == 1;

    static bool NoEventOutcome(SemanticSpecification spec) =>
        !spec.ThenDenied && spec.ThenErrors.IsEmpty && spec.ThenEvents.IsEmpty && !spec.ThenNoEvents;

    static bool ResultsSupported(SemanticSpecificationQueryResult expected, SemanticReadModel model) =>
        !expected.Exactly && expected.Results.Length == 1 && expected.Results.All(result => result.ReadModel == model.Id && !result.Exactly && !result.Values.IsEmpty && result.Values.All(value => model.Properties.Any(property => property.Id == value.TargetProperty)));

    static bool Handles(ProjectionScopeNode root, SemanticSpecificationEvent given) =>
        root.Scope.From.Any(from => from.EventContract == given.EventContract && from.Key is SemanticProjectionValueKey { Value: SemanticProjectionEventSourceIdentity });

    static bool KeySupported(SemanticKeyedQuery query, SemanticSpecificationQueryResult expected) =>
        query.Argument is not null && query.KeyProperty is not null && query.KeyParts.IsEmpty && expected.Key is not SemanticNullValue;

    QueryPlan? Plan(SemanticSpecification spec)
    {
        var expected = spec.ThenQueries[0];
        var query = located.Slice.Queries.SingleOrDefault(candidate => candidate.Id == expected.Query);
        if (query is null || !QuerySupported(query, expected)) return null;
        var projection = Projection(query);
        return projection is null ? null : Plan(spec, query, projection);
    }

    ModelProjection? Projection(SemanticKeyedQuery query)
    {
        var model = located.Slice.ReadModels.SingleOrDefault(candidate => candidate.Id == query.ReadModel);
        var projection = located.Slice.Projections.SingleOrDefault(candidate => candidate.ReadModel == query.ReadModel);
        if (model is null || projection is null) return null;
        if (!ProjectionHandlerShape.RootOnly(projection) || ProjectionHandlerShape.Scoped(projection)) return null;
        return new(model, projection, ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, JavaNames.Type(model.Name)));
    }

    QueryPlan? Plan(SemanticSpecification spec, SemanticKeyedQuery query, ModelProjection projection)
    {
        if (!ResultsSupported(spec.ThenQueries[0], projection.Model)) return null;
        if (!spec.GivenEvents.All(given => Handles(projection.Root, given))) return null;
        var events = spec.GivenEvents.Select(given => AxonViewEvents.Normalize(context, given)).ToArray();
        if (events.Any(given => given is null)) return null;
        return new(spec, query, projection, [.. events.OfType<SemanticSpecificationEvent>()]);
    }

    bool QuerySupported(SemanticKeyedQuery query, SemanticSpecificationQueryResult expected) =>
        KeySupported(query, expected) && query.Authorization is null && !QueryPerformer.Required(context, query) && query.Filters.IsEmpty && expected.Filters.IsEmpty;

    void Emit(QueryPlan plan)
    {
        var package = types.Package(located);
        var name = JavaNames.Type(plan.Spec.Name) + "QuerySpecificationsTest";
        var modelName = JavaNames.Type(plan.Projection.Model.Name);
        var queryName = JavaNames.Type(plan.Query.Name);
        var projectionName = JavaNames.Type(plan.Projection.Projection.Name);
        var code = new JavaCodeBuilder(package).Declare(name).Declare(modelName).Declare(queryName).Declare(queryName + "Handler").Declare(projectionName + "Projector");
        ViewRepositories.Declare(code, plan.Projection.Model);
        var claims = new ViewSpecificationClaims(context, types);
        foreach (var given in plan.Events) claims.Event(given, code, true);
        new QueryShape(plan.Query, plan.Projection.Model, types).Claim(code);
        ViewRepositories.Annotate(code);
        code.Block("class " + name, body => ViewRepositories.Field(body, plan.Projection.Model).Line("@" + body.Reference("org.junit.jupiter.api.Test"))
            .Line("@" + body.Reference("org.junit.jupiter.api.DisplayName") + "(" + JavaCodeBuilder.StringLiteral(plan.Spec.Id.ToString()) + ")")
            .Block("void specification()", method => Scenario(method, plan)));
        AxonSpecificationsEmitter.Register(context, plan.Spec.Id, package + "." + name, "specification", null);
        context.AddJava(package, name, code.Build(), test: true, sources: [plan.Spec.Id]);
    }

    void Scenario(JavaCodeBuilder code, QueryPlan plan)
    {
        var model = plan.Projection.Model;
        var projection = plan.Projection.Projection;
        code.Line($"var projector = new {JavaNames.Type(projection.Name)}Projector(repository);");
        var arguments = ProjectionHandlerShape.Arguments(projection, new(context, types, model.Id));
        foreach (var given in plan.Events) code.Line($"projector.on({AxonViewEvents.Expression(context, types, given, code)}{arguments});");
        var key = types.Literal(plan.Spec.ThenQueries[0].Key, plan.Query.Argument!.Type, code);
        code.Line($"var actual = new {JavaNames.Type(plan.Query.Name)}Handler(repository).handle(new {JavaNames.Type(plan.Query.Name)}({key})).rows();");
        foreach (var result in plan.Spec.ThenQueries[0].Results) Assert(code, result, model);
    }

    void Assert(JavaCodeBuilder code, SemanticSpecificationReadModel expected, SemanticReadModel model)
    {
        var matcher = new ViewExpectations(context.Request.Model.Application, types, code);
        var predicates = expected.Values.Select(value =>
        {
            var property = model.Properties.Single(candidate => candidate.Id == value.TargetProperty);
            return matcher.Predicate(value.Value, property.Type, $"row.{JavaNames.Member(property.Name)}()");
        });
        code.Line($"{code.Reference("org.junit.jupiter.api.Assertions")}.assertTrue(actual.stream().anyMatch(row -> {string.Join(" && ", predicates)}), \"Expected modeled query result properties\");");
    }

    sealed record ModelProjection(SemanticReadModel Model, SemanticProjection Projection, ProjectionScopeNode Root);
    sealed record QueryPlan(SemanticSpecification Spec, SemanticKeyedQuery Query, ModelProjection Projection, SemanticSpecificationEvent[] Events);
}
