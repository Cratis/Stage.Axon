// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

/// <summary>
/// Emits the generated native proof that a rolled-back unit of work leaves a scoped or reducer projector's rows and scope state untouched.
/// </summary>
/// <param name="context">The emission context.</param>
/// <param name="types">The Java types of the application.</param>
internal sealed class ProjectorRollbackTests(AxonEmissionContext context, ViewJavaTypes types)
{
    /// <summary>
    /// Emits the rollback proof for one projector when a specification can drive it.
    /// </summary>
    /// <param name="owner">The slice that owns the projector.</param>
    /// <param name="model">The read model the projector maintains.</param>
    /// <param name="projector">The Java name of the projector.</param>
    /// <param name="construction">The Java expression constructing the projector from a variable named <c>store</c>.</param>
    /// <param name="observed">The event contracts the projector observes.</param>
    internal void Emit(LocatedSemanticSlice owner, SemanticReadModel model, string projector, string construction, IReadOnlyCollection<SemanticId> observed)
    {
        var specifications = Eligible(owner, model, observed);
        if (specifications.Count == 0) return;
        var package = types.Package(owner);
        var name = projector + "RollbackTest";
        var modelName = JavaNames.Type(model.Name);
        var updates = new ProjectionUpdates(context, types, model.Id).Enabled ? ", null" : string.Empty;
        var query = Query(owner, model);
        var methods = new HashSet<string>(StringComparer.Ordinal);
        var tests = new System.Text.StringBuilder();
        foreach (var specification in specifications)
        {
            var method = JavaNames.Member(specification.Name);
            while (!methods.Add(method)) method += "Again";
            tests.Append(Specification(specification, method, query, updates));
        }
        tests.Append(Concurrency(specifications[0], updates));
        var source = ProjectorRollbackProof.Source
            .Replace("__TESTS__", tests.ToString().TrimEnd('\n'), StringComparison.Ordinal)
            .Replace("__PACKAGE__", package, StringComparison.Ordinal)
            .Replace("__RUNTIME__", types.BasePackage + ".runtime", StringComparison.Ordinal)
            .Replace("__NAME__", name, StringComparison.Ordinal)
            .Replace("__PROJECTOR__", projector, StringComparison.Ordinal)
            .Replace("__CONSTRUCTION__", construction, StringComparison.Ordinal)
            .Replace("__MODEL__", modelName, StringComparison.Ordinal);
        context.Artifacts.Add(PlannedArtifact.CreateText(
            $"src/test/java/{package.Replace('.', '/')}/{name}.java",
            PersistentJavaTemplate.Render(source),
            [model.Id, .. specifications.Select(specification => specification.Id)]));
    }

    static string Statements(IEnumerable<string> events, string indent, Func<string, string> call) =>
        string.Join('\n', events.Select(expression => $"{indent}{{ var event = {expression}; {call("event")} }}"));

    string Specification(SemanticSpecification specification, string method, SemanticKeyedQuery? query, string updates)
    {
        var events = Events(specification);
        var committed = events.Count >= 2 ? events.GetRange(events.Count - 1, 1) : events;
        var rolled = events.Count >= 2 ? events.GetRange(0, events.Count - 1) : events;
        var handler = query is null ? string.Empty : $"new {JavaNames.Type(query.Name)}Handler";
        var type = query is null ? string.Empty : JavaNames.Type(query.Name);
        return ProjectorRollbackProof.Specification
            .Replace("__METHOD__", method, StringComparison.Ordinal)
            .Replace("__COMMITTED_REFERENCE__", Statements(committed, "        ", @event => $"oracle.on({@event}, message({@event}){updates});"), StringComparison.Ordinal)
            .Replace("__ROLLED__", Statements(rolled, "            ", @event => $"projector.handle({@event}{updates}, context(context, {@event}));"), StringComparison.Ordinal)
            .Replace("__COMMITTED__", $"        run(context -> {{\n{Statements(committed, "            ", @event => $"projector.handle({@event}{updates}, context(context, {@event}));")}\n        }});", StringComparison.Ordinal)
            .Replace("__EMPTY_SNAPSHOT__", query is null ? string.Empty : $"        Assertions.assertTrue({handler}(store).handle(new {type}()).rows().isEmpty(), \"A rolled-back unit of work must leave the query snapshot empty\");", StringComparison.Ordinal)
            .Replace("__SNAPSHOT__", query is null ? string.Empty : $"        Assertions.assertEquals(Set.copyOf({handler}(expected).handle(new {type}()).rows()), Set.copyOf({handler}(store).handle(new {type}()).rows()), \"rolled-back events must leave no scope state behind\");", StringComparison.Ordinal);
    }

    string Concurrency(SemanticSpecification specification, string updates)
    {
        var events = Events(specification);
        string Handle(string indent) => Statements(events, indent, @event => $"projector.handle({@event}{updates}, context(context, {@event}));");
        return ProjectorRollbackProof.Concurrency
            .Replace("__ALL_FIRST__", Handle("                        "), StringComparison.Ordinal)
            .Replace("__ALL_SECOND__", Handle("                "), StringComparison.Ordinal)
            .Replace("__ORACLE__", Statements(events, "            ", @event => $"oracle.on({@event}, message({@event}){updates});"), StringComparison.Ordinal);
    }

    List<string> Events(SemanticSpecification specification) =>
        [.. specification.GivenEvents.Select(given => AxonViewEvents.Expression(context, types, AxonViewEvents.Normalize(context, given)!))];

    List<SemanticSpecification> Eligible(LocatedSemanticSlice owner, SemanticReadModel model, IReadOnlyCollection<SemanticId> observed) =>
    [.. owner.Slice.Specifications.Where(specification => ViewSpecificationSupport.Legacy(specification, model, types) &&
        !specification.GivenEvents.IsEmpty &&
        specification.GivenEvents.All(given => observed.Contains(given.EventContract) && AxonViewEvents.Normalize(context, given) is not null))];

    SemanticKeyedQuery? Query(LocatedSemanticSlice owner, SemanticReadModel model) => owner.Slice.Queries.FirstOrDefault(query =>
        query.ReadModel == model.Id && query.Cardinality == SemanticQueryCardinality.Many && query.Delivery != SemanticQueryDelivery.Live &&
        !QueryPerformer.Required(context, query) && !new QueryShape(query, model, types).Arguments.Any());
}
