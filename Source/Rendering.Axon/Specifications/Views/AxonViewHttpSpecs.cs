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
        var response = new AxonHttpResponse(200) { AssertionFragments = [Assertion(specification)] };
        AxonHttpTests.Register(
            context,
            new(
                specification.Id,
                [new(new("GET", QueryEndpoint.Route(candidate.Owner, candidate.Query)), response)],
                new(string.Empty, [], false))
            { ArrangeFragments = Arrange(projection, specification) });
    }

    static bool Observes(SemanticProjectionScope scope, SemanticId @event) => scope.From.Any(from => from.EventContract == @event)
        || scope.Joins.Any(join => join.EventContract == @event)
        || scope.Removals.Any(removal => removal.EventContract == @event)
        || scope.JoinRemovals.Any(removal => removal.EventContract == @event)
        || scope.Every is { SubscribesToAllEvents: true };

    bool Supports(SemanticSpecification specification)
    {
        if (context.Specifications[specification.Id].DisabledCode is not null || specification.WhenAppended is not null) return false;
        if (specification.ThenReadModels.IsEmpty) return false;
        return specification.ThenAbsentReadModels.IsEmpty;
    }

    ImmutableArray<AxonHttpTests.Fragment> Arrange(SemanticProjection projection, SemanticSpecification specification)
    {
        var modelType = types.Package(located) + "." + JavaNames.Type(model.Name);
        var projector = types.Package(located) + "." + JavaNames.Type(projection.Name) + "Projector";
        var root = ProjectionScopeNode.Create(context.Request.Model.Application, types, projection, model, modelType);
        var arguments = ProjectionHandlerShape.Arguments(projection, new(context, types, model.Id));
        var lines = new List<AxonHttpTests.Fragment>
        {
            new(code => $"application.getBean({JavaCodeBuilder.StringLiteral(modelType)}, {code.Reference(types.BasePackage + ".runtime.ReadModelStore")}.class).clear();", code =>
            {
                code.Reference(modelType);
                code.Reference(projector);
            }),
            new(code => $"var projector = application.getBean({code.Reference(projector)}.class);")
        };
        foreach (var given in specification.GivenEvents.Where(given => root.All.Any(node => Observes(node.Scope, given.EventContract))))
            lines.Add(new(code => $"projector.on({AxonViewEvents.Expression(context, types, given, code)}{arguments});", code => new ViewSpecificationClaims(context, types).Event(given, code)));
        return [.. lines];
    }

    AxonHttpTests.Fragment Assertion(SemanticSpecification specification)
    {
        var modelType = types.Package(located) + "." + JavaNames.Type(model.Name);
        return new(
            code =>
            {
                var checks = Predicates(specification, code).Select(predicate =>
                    $"{code.Reference("org.junit.jupiter.api.Assertions")}.assertTrue(rows.stream().anyMatch(row -> {predicate}), \"Expected modeled HTTP read-model properties\");");
                return $"result -> {{ var mapper = application.getBean({code.Reference("tools.jackson.databind.json.JsonMapper")}.class); var rows = mapper.readValue(result.getResponse().getContentAsString(), new {code.Reference("tools.jackson.core.type.TypeReference")}<{code.Reference("java.util.List")}<{code.Reference(modelType)}>>() {{}}); {string.Join(' ', checks)} }}";
            },
            code => code.ClaimExpressions([modelType, .. Predicates(specification)]));
    }

    IEnumerable<string> Predicates(SemanticSpecification specification, JavaCodeBuilder? code = null)
    {
        var matcher = new ViewExpectations(context.Request.Model.Application, types, code);
        return specification.ThenReadModels.Select(expected =>
        {
            var predicates = expected.Values.Select(value =>
            {
                var property = model.Properties.First(candidate => candidate.Id == value.TargetProperty);
                return matcher.Predicate(value.Value, property.Type, "row." + JavaNames.Member(property.Name) + "()");
            });
            return string.Join(" && ", predicates.DefaultIfEmpty("true"));
        });
    }
}
