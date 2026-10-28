// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Persistence;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionScopeSerializationTests(AxonEmissionContext context, ViewJavaTypes types)
{
    internal void Emit(LocatedSemanticSlice owner, SemanticReadModel model, string projector, ProjectionScopeNode root)
    {
        var specification = owner.Slice.Specifications.FirstOrDefault(candidate => candidate.Name == "ChildAndJoinBeforeParent" &&
            candidate.ThenReadModels.Any(expected => expected.ReadModel == model.Id) && candidate.GivenEvents.Length == 3);
        if (specification is null) return;
        var events = specification.GivenEvents.Select(given => AxonViewEvents.Normalize(context, given)).ToArray();
        if (events.Any(@event => @event is null)) return;
        var expressions = events.Select(@event => AxonViewEvents.Expression(context, types, @event!)).ToArray();
        var caches = root.All.SelectMany(node => Enumerable.Range(0, node.Scope.Joins.Length).Select(index => ScopedProjectionEmitter.Cache(node, index))).ToArray();
        if (caches.Length == 0) return;
        var package = types.Package(owner);
        var name = projector + "SerializationTest";
        var updates = new ProjectionUpdates(context, types, model.Id).DefaultArgument;
        var source = ProjectionScopeSerializationProof.Source
            .Replace("__SCENARIOS__", Scenarios(owner, model), StringComparison.Ordinal)
            .Replace("__PACKAGE__", package, StringComparison.Ordinal)
            .Replace("__NAME__", name, StringComparison.Ordinal)
            .Replace("__PROJECTOR__", projector, StringComparison.Ordinal)
            .Replace("__MODEL__", JavaNames.Type(model.Name), StringComparison.Ordinal)
            .Replace("__STATE__", ScopedProjectionEmitter.StateType(root), StringComparison.Ordinal)
            .Replace("__STORE__", AxonRuntimePackage.Type(types.BasePackage, "ReadModelStore"), StringComparison.Ordinal)
            .Replace("__MEMORY__", AxonRuntimePackage.Type(types.BasePackage, "InMemoryReadModelStore"), StringComparison.Ordinal)
            .Replace("__SUPPORT__", AxonRuntimePackage.Type(types.BasePackage, "TestSupport"), StringComparison.Ordinal)
            .Replace("__DEFERRED_KIND__", ProjectionScopeState.Deferred(ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).First(@event => @event.Id == specification.GivenEvents[1].EventContract)), StringComparison.Ordinal)
            .Replace("__JOIN__", expressions[0], StringComparison.Ordinal)
            .Replace("__CHILD__", expressions[1], StringComparison.Ordinal)
            .Replace("__PARENT__", expressions[2], StringComparison.Ordinal)
            .Replace("__UPDATES__", updates, StringComparison.Ordinal)
            .Replace("__CACHE__", caches[0], StringComparison.Ordinal)
            .Replace("__CACHES__", string.Concat(caches.Select((cache, index) => index == 0 ? ", cache" : ", original." + cache + "()")), StringComparison.Ordinal);
        context.Artifacts.Add(PlannedArtifact.CreateText($"src/test/java/{package.Replace('.', '/')}/{name}.java", PersistentJavaTemplate.Render(source), [model.Id, specification.Id]));
    }

    string Scenarios(LocatedSemanticSlice owner, SemanticReadModel model)
    {
        var updates = new ProjectionUpdates(context, types, model.Id).DefaultArgument;
        var source = string.Join('\n', owner.Slice.Specifications.Where(specification =>
                ViewSpecificationSupport.Legacy(specification, model, types) && !specification.GivenEvents.IsEmpty &&
                specification.GivenEvents.All(given => AxonViewEvents.Normalize(context, given) is not null))
            .Select(specification => $$"""
                @Test
                void {{JavaNames.Member(specification.Name)}}SurvivesRoundTrips() throws Exception {
                    var store = new InMemoryReadModelStore<Object, __MODEL__>();
                    var projector = new __PROJECTOR__(store);
                    var recovered = fresh();
                {{string.Join('\n', specification.GivenEvents.Select(given => $$"""
                    {
                        var event = {{AxonViewEvents.Expression(context, types, AxonViewEvents.Normalize(context, given)!)}};
                        recovered = roundTrip(recovered);
                        projector.on(event, message(event){{updates}});
                        recovered.projector().on(event, message(event){{updates}});
                        Assertions.assertEquals(Set.copyOf(store.all()), Set.copyOf(recovered.store().all()));
                        Assertions.assertEquals(scope(projector).get("scope"), scope(recovered.projector()).get("scope"));
                    }
                """))}}
                }
                """));
        return string.Join('\n', source.Split('\n').Select(line => "    " + line));
    }
}
