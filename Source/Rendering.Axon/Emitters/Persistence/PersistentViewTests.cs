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
        var projection = owner.Slice.Projections.First(candidate => candidate.ReadModel == model.Id);
        var spec = Specification(owner, model, projection);
        var query = Query(owner, model);
        if (spec is null || query is null) return;
        var package = types.Package(owner);
        var modelName = JavaNames.Type(model.Name);
        var name = modelName + "Persistent";
        var updates = new ProjectionUpdates(context, types, model.Id);
        var events = spec.GivenEvents.Select(given => AxonViewEvents.Expression(context, types, given)).ToArray();
        string Render(string source) => source.Replace("__PACKAGE__", package, StringComparison.Ordinal)
            .Replace("__RUNTIME__", types.BasePackage + ".runtime", StringComparison.Ordinal)
            .Replace("__PERSISTENCE__", persistence, StringComparison.Ordinal)
            .Replace("__APPLICATION__", types.BasePackage, StringComparison.Ordinal)
            .Replace("__MODEL__", package + "." + modelName, StringComparison.Ordinal).Replace("__NAME__", name, StringComparison.Ordinal)
            .Replace("__QUALIFIER__", package + "." + modelName, StringComparison.Ordinal)
            .Replace("__PROJECTOR__", package + "." + JavaNames.Type(projection.Name) + "Projector", StringComparison.Ordinal)
            .Replace("__IMPORTS__", Imports(model, projection, query), StringComparison.Ordinal)
            .Replace("__ROUTE__", QueryEndpoint.Route(owner, query), StringComparison.Ordinal)
            .Replace("__EVENT__", events[0], StringComparison.Ordinal)
            .Replace("__EVENT_TYPE__", types.EventType(spec.GivenEvents[0].EventContract), StringComparison.Ordinal)
            .Replace("__COUNT__", spec.GivenEvents.Count(given => given.EventContract == spec.GivenEvents[0].EventContract).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__APPENDS__", string.Join('\n', events.Select(expression => $"            append(application, {expression});")), StringComparison.Ordinal)
            .Replace("__EXPECTED__", string.Join('\n', events.Select(expression => $"        projector.on({expression}{updates.DefaultArgument});")), StringComparison.Ordinal)
            .Replace("__ARGUMENT__", updates.DefaultArgument, StringComparison.Ordinal)
            .Replace("__TAGS__", Tags(spec), StringComparison.Ordinal)
            .Replace("__FIXTURE_EVENTS__", string.Join(", ", events), StringComparison.Ordinal);
        void Add(string suffix, string source) => context.Artifacts.Add(PlannedArtifact.CreateText(
            $"src/test/java/{package.Replace('.', '/')}/{name}{suffix}.java", Render(source), [model.Id, spec.Id]));
        Add("Support", PersistentViewProofSupport.Source);
        Add("Fixture", PersistentViewFixtureContract.Source);
        Add("Test", PersistentViewProof.Source);
        Add("MultiSegmentTest", PersistentViewProof.MultiSegmentSource);
        Add("StoreTest", PersistentViewContractTests.Source);
        Add("LegacyTest", PersistentViewLegacyProof.Source);
        Add("EnvelopeTest", PersistentViewEnvelopeTests.Source);
        var live = LiveQuery(owner, model);
        if (live is not null && ProjectionTransitions.From(projection).Count(from => from.EventContract == spec.GivenEvents[0].EventContract) == 1)
            Add("DefaultDispatchTest", DefaultViewDispatchProof.Source.Replace("__LIVE_QUERY__", package + "." + JavaNames.Type(live.Name), StringComparison.Ordinal));
    }

    string Tags(SemanticSpecification spec) => string.Join('\n', spec.GivenEvents.Select(given => given.EventContract).Distinct().Select(id =>
    {
        var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).First(contract => contract.Id == id);
        var tag = AxonEventSources.PrimaryTag(context, contract);
        return $"        if (event instanceof {types.EventType(id)} tagged) return new Tag({JavaCodeBuilder.StringLiteral(tag.TagKey)}, tagged.{tag.ComponentName}().toString());";
    }));

    SemanticSpecification? Specification(LocatedSemanticSlice owner, SemanticReadModel model, SemanticProjection projection)
    {
        var observed = ProjectionTransitions.From(projection).Select(from => from.EventContract).ToHashSet();
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

    string Imports(SemanticReadModel model, SemanticProjection projection, SemanticKeyedQuery query) => string.Join(", ", new[]
    {
        types.BasePackage + ".infrastructure.AxonConfiguration.class", types.BasePackage + ".infrastructure.SecurityConfiguration.class",
        types.BasePackage + ".runtime.persistent.PersistentConfiguration.class", persistence + ".PersistentViewConfiguration.class",
        JavaNames.Type(model.Name) + "StoreConfiguration.class", JavaNames.Type(projection.Name) + "Projector.class",
        JavaNames.Type(query.Name) + "Handler.class", JavaNames.Type(query.Name) + "Endpoint.class"
    });
}
