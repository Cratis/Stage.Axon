// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionSequencing(AxonEmissionContext context, ViewJavaTypes types, SemanticReadModel model)
{
    internal bool UsesModelSequence => ScopeSelection.Locate(context.Request.Model.Application)
        .SelectMany(owner => owner.Slice.Projections)
        .Where(projection => projection.ReadModel == model.Id)
        .SelectMany(projection => ProjectionTransitions.From(projection).GroupBy(transition => transition.EventContract))
        .Any(NeedsFallback);

    internal string Namespace => types.Package(ScopeSelection.Locate(context.Request.Model.Application)
        .Single(owner => owner.Slice.ReadModels.Any(candidate => candidate.Id == model.Id))) + "." + JavaNames.Type(model.Name);

    internal ProjectionHandlerBinding.ProjectionSequence Projection(IGrouping<SemanticId, SemanticProjectionFrom> group, JavaCodeBuilder code, ProjectionPolicyNames names)
    {
        var contract = Contract(group.Key);
        var key = UsesModelSequence || group.Count() != 1 ? null : new ProjectionValues(context, types, code).Key(group.Single().Key, contract);
        return new(names.For(group.Key), types.EventType(group.Key, code), key);
    }

    // Reducers write one source-keyed row, but inherit any other writer's model-wide fallback.
    internal ProjectionHandlerBinding.ProjectionSequence Reducer(SemanticEventContract contract, JavaCodeBuilder code, ProjectionPolicyNames names) =>
        new(names.For(contract.Id), types.EventType(contract.Id, code), UsesModelSequence ? null : new ProjectionValues(context, types, code).Source(contract));

    bool NeedsFallback(IGrouping<SemanticId, SemanticProjectionFrom> group) =>
        group.Count() != 1 || new ProjectionValues(context, types).Key(group.Single().Key, Contract(group.Key)) is null;

    SemanticEventContract Contract(SemanticId id) => ScopeSelection.Locate(context.Request.Model.Application)
        .SelectMany(owner => owner.Slice.Events).Single(contract => contract.Id == id);
}
