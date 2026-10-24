// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views;

internal sealed class AxonViewHttpSpecs(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticReadModel model)
{
    internal void Register(SemanticProjection projection, SemanticSpecification specification)
    {
        if (!Supports(specification)) return;
        var candidates = ScopeSelection.Select(context.Request).SelectMany(owner => owner.Slice.Queries
            .Where(query => query.ReadModel == model.Id && query.Delivery != SemanticQueryDelivery.Live && query.Cardinality == SemanticQueryCardinality.Many && query.Authorization is null)
            .Select(query => (Owner: owner, Query: query)));
        var candidate = candidates.FirstOrDefault(entry => !QueryPerformer.Required(context, entry.Query) && !new QueryShape(entry.Query, model, types).Arguments.Any());
        if (candidate.Query is null) return;
        var response = new AxonHttpResponse(200) { Assertions = [Assertion(specification)] };
        AxonHttpTests.Register(
            context,
            new(
                specification.Id,
                [new(new("GET", QueryEndpoint.Route(candidate.Owner, candidate.Query)), response)],
                new(string.Empty, [], false))
            { Arrange = Arrange(projection, specification) });
    }

    static bool Observes(SemanticProjectionScope scope, SemanticId @event) => scope.From.Any(from => from.EventContract == @event)
        || scope.Joins.Any(join => join.EventContract == @event)
        || scope.Removals.Any(removal => removal.EventContract == @event)
        || scope.JoinRemovals.Any(removal => removal.EventContract == @event)
        || scope.Every is { SubscribesToAllEvents: true };

    bool Supports(SemanticSpecification specification)
    {
        if (context.Specifications[specification.Id].DisabledCode is not null) return false;
        if (specification.ThenReadModels.IsEmpty) return false;
        return specification.ThenAbsentReadModels.IsEmpty;
    }

    ImmutableArray<string> Arrange(SemanticProjection projection, SemanticSpecification specification)
    {
        var modelType = types.Package(located) + "." + JavaNames.Type(model.Name);
        var projector = types.Package(located) + "." + JavaNames.Type(projection.Name) + "Projector";
        var root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, modelType);
        var lines = new List<string>
        {
            $"application.getBean({JavaCodeBuilder.StringLiteral(modelType)}, {types.BasePackage}.runtime.ReadModelStore.class).clear();",
            $"var projector = application.getBean({projector}.class);"
        };
        foreach (var given in specification.GivenEvents.Where(given => root.All.Any(node => Observes(node.Scope, given.EventContract))))
            lines.Add($"projector.on({AxonViewEvents.Expression(context, types, given)});");
        return [.. lines];
    }

    string Assertion(SemanticSpecification specification)
    {
        var modelType = types.Package(located) + "." + JavaNames.Type(model.Name);
        var matcher = new ViewExpectations(context.Request.Model.Application, types);
        var checks = specification.ThenReadModels.Select(expected =>
        {
            var predicates = expected.Values.Select(value =>
            {
                var property = model.Properties.First(candidate => candidate.Id == value.TargetProperty);
                return matcher.Predicate(value.Value, property.Type, "row." + JavaNames.Member(property.Name) + "()");
            });
            return $"org.junit.jupiter.api.Assertions.assertTrue(rows.stream().anyMatch(row -> {string.Join(" && ", predicates.DefaultIfEmpty("true"))}), \"Expected modeled HTTP read-model properties\");";
        });
        return $"result -> {{ var mapper = application.getBean(tools.jackson.databind.json.JsonMapper.class); var rows = mapper.readValue(result.getResponse().getContentAsString(), new tools.jackson.core.type.TypeReference<java.util.List<{modelType}>>() {{}}); {string.Join(' ', checks)} }}";
    }
}
