// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal sealed class PersistentViewTests(AxonEmissionContext context, ViewJavaTypes types, string persistence)
{
    internal void Emit()
    {
        foreach (var owner in ScopeSelection.Select(context.Request))
        {
            foreach (var model in owner.Slice.ReadModels.Where(model => PersistentViewAdmission.Supports(context, model.Id)))
                EmitModel(owner, model);
        }
    }

    void EmitModel(LocatedSemanticSlice owner, SemanticReadModel model)
    {
        var target = ProjectorInputs(owner, model);
        var spec = Specification(owner, model, target.Observed);
        var query = Query(owner, model);
        if (spec is null || query is null) return;
        var package = types.Package(owner);
        var modelName = JavaNames.Type(model.Name);
        var name = modelName + "Persistent";
        var events = spec.GivenEvents.Select(given => AxonViewEvents.Expression(context, types, given)).ToArray();
        string Render(string source) => source.Replace("__PACKAGE__", package, StringComparison.Ordinal)
            .Replace("__RUNTIME__", types.BasePackage + ".runtime", StringComparison.Ordinal)
            .Replace("__PERSISTENCE__", persistence, StringComparison.Ordinal)
            .Replace("__APPLICATION__", types.BasePackage, StringComparison.Ordinal)
            .Replace("__MODEL__", package + "." + modelName, StringComparison.Ordinal).Replace("__NAME__", name, StringComparison.Ordinal)
            .Replace("__QUALIFIER__", package + "." + modelName, StringComparison.Ordinal)
            .Replace("__PROJECTOR__", package + "." + target.Projector, StringComparison.Ordinal)
            .Replace("__REDUCER_ARGUMENT__", target.ReducerArgument, StringComparison.Ordinal)
            .Replace("__REDUCER_IMPORTS__", target.ReducerImports, StringComparison.Ordinal)
            .Replace("__IMPORTS__", Imports(package, query), StringComparison.Ordinal)
            .Replace("__ROUTE__", QueryEndpoint.Route(owner, query), StringComparison.Ordinal)
            .Replace("__EVENT__", events[0], StringComparison.Ordinal)
            .Replace("__EVENT_TYPE__", types.EventType(spec.GivenEvents[0].EventContract), StringComparison.Ordinal)
            .Replace("__COUNT__", spec.GivenEvents.Count(given => given.EventContract == spec.GivenEvents[0].EventContract).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__APPENDS__", string.Join('\n', events.Select(expression => $"            append(application, {expression});")), StringComparison.Ordinal)
            .Replace("__EXPECTED__", string.Join('\n', events.Select(expression => $"        projector.on({expression}{target.Arguments});")), StringComparison.Ordinal)
            .Replace("__ARGUMENT__", target.Arguments, StringComparison.Ordinal)
            .Replace("__NORMALIZE__", PersistentViewProofNormalization.Body(context.Request.Model.Application, types, model, owner.Slice.Projections.Where(candidate => candidate.ReadModel == model.Id)), StringComparison.Ordinal)
            .Replace("__TAGS__", Tags(spec), StringComparison.Ordinal)
            .Replace("__FIXTURE_EVENTS__", string.Join(", ", events), StringComparison.Ordinal);
        void Add(string suffix, string source) => context.Artifacts.Add(PlannedArtifact.CreateText(
            $"src/test/java/{package.Replace('.', '/')}/{name}{suffix}.java", PersistentJavaTemplate.Render(Render(source)), [model.Id, spec.Id]));
        Add("Support", PersistentViewProofSupport.Source);
        Add("Fixture", PersistentViewFixtureContract.Source);
        Add("Test", PersistentViewProof.Source);
        Add("MultiSegmentTest", PersistentViewProof.MultiSegmentSource);
        Add("StoreTest", PersistentViewContractTests.Source);
        Add("LegacyTest", PersistentViewLegacyProof.Source);
        Add("EnvelopeTest", PersistentViewEnvelopeTests.Source);
        var live = LiveQuery(owner, model);
        if (live is not null && target.Observed.Count(contract => contract == spec.GivenEvents[0].EventContract) == 1)
        {
            var transition = owner.Slice.Projections.Where(projection => projection.ReadModel == model.Id).SelectMany(ProjectionTransitions.From)
                .Single(from => from.EventContract == spec.GivenEvents[0].EventContract);
            var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == transition.EventContract);
            Add("DefaultDispatchTest", DefaultViewDispatchProof.Source
                .Replace("__ROLLBACK_PROOF__", DefaultViewRollbackProof.Source, StringComparison.Ordinal)
                .Replace("__KEY__", new ProjectionValues(context, types).Key(transition.Key, @event), StringComparison.Ordinal)
                .Replace("__LIVE_QUERY__", package + "." + JavaNames.Type(live.Name), StringComparison.Ordinal));
        }
    }

    (string Projector, string Arguments, string ReducerArgument, string ReducerImports, SemanticId[] Observed) ProjectorInputs(LocatedSemanticSlice owner, SemanticReadModel model)
    {
        var updates = new ProjectionUpdates(context, types, model.Id);
        if (owner.Slice.Projections.FirstOrDefault(candidate => candidate.ReadModel == model.Id) is { } projection)
            return (JavaNames.Type(projection.Name) + "Projector", ProjectionHandlerShape.Arguments(projection, updates), string.Empty, string.Empty, [.. ProjectionTransitions.From(projection).Select(from => from.EventContract)]);
        var reducer = owner.Slice.Reducers.Single(candidate => candidate.ReadModel == model.Id);
        var name = JavaNames.Type(model.Name);
        var qualifiedName = types.Package(owner) + "." + name;
        return (name + "ReducerProjector", ", null" + updates.DefaultArgument, $", new {qualifiedName}Reducer() {{}}", $", {qualifiedName}ReducerConfiguration.class", [.. reducer.Transitions.Select(transition => transition.EventContract)]);
    }

    string Tags(SemanticSpecification spec) => string.Join('\n', spec.GivenEvents.Select(given => given.EventContract).Distinct().Select(id =>
    {
        var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).First(contract => contract.Id == id);
        var tag = AxonEventSources.PrimaryTag(context, contract);
        return $"        if (event instanceof {types.EventType(id)} tagged) return new Tag({JavaCodeBuilder.StringLiteral(tag.TagKey)}, tagged.{tag.ComponentName}().toString());";
    }));

    SemanticSpecification? Specification(LocatedSemanticSlice owner, SemanticReadModel model, IReadOnlyCollection<SemanticId> observed)
    {
        return owner.Slice.Specifications.FirstOrDefault(spec => ViewSpecificationSupport.Legacy(spec, model, types) &&
            !spec.GivenEvents.IsEmpty && spec.ThenReadModels.Any(expected => expected.ReadModel == model.Id) &&
            spec.GivenEvents.All(given => observed.Contains(given.EventContract)));
    }

    SemanticKeyedQuery? Query(LocatedSemanticSlice owner, SemanticReadModel model) => owner.Slice.Queries.FirstOrDefault(query =>
        query.ReadModel == model.Id && query.Cardinality == SemanticQueryCardinality.Many && query.Delivery != SemanticQueryDelivery.Live &&
        !QueryPerformer.Required(context, query) && !new QueryShape(query, model, types).Arguments.Any());

    SemanticKeyedQuery? LiveQuery(LocatedSemanticSlice owner, SemanticReadModel model) => owner.Slice.Queries.FirstOrDefault(query =>
        query.ReadModel == model.Id && query.Cardinality == SemanticQueryCardinality.Many && query.Delivery == SemanticQueryDelivery.Live && query.Authorization is null &&
        !QueryPerformer.Required(context, query) && !new QueryShape(query, model, types).Arguments.Any());

    string Imports(string package, SemanticKeyedQuery query) => string.Join(", ", new[]
    {
        types.BasePackage + ".infrastructure.AxonConfiguration.class", types.BasePackage + ".infrastructure.SecurityConfiguration.class",
        types.BasePackage + ".runtime.persistent.PersistentConfiguration.class", persistence + ".PersistentViewConfiguration.class",
        package + "." + JavaNames.Type(query.Name) + "Handler.class", package + "." + JavaNames.Type(query.Name) + "Endpoint.class"
    }.Concat(PersistentProjectorImports()).Distinct(StringComparer.Ordinal));

    IEnumerable<string> PersistentProjectorImports()
    {
        foreach (var owner in ScopeSelection.Select(context.Request))
        {
            foreach (var model in owner.Slice.ReadModels.Where(model => PersistentViewAdmission.Supports(context, model.Id)))
            {
                var package = types.Package(owner);
                var target = ProjectorInputs(owner, model);
                yield return package + "." + JavaNames.Type(model.Name) + "StoreConfiguration.class";
                yield return package + "." + target.Projector + ".class";
                if (owner.Slice.Reducers.Any(reducer => reducer.ReadModel == model.Id))
                    yield return package + "." + JavaNames.Type(model.Name) + "ReducerConfiguration.class";
            }
        }
    }
}
