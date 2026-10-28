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
        .Any(projection => projection.ReadModel == model.Id && NeedsFallback(projection));

    internal string Namespace => types.Package(ScopeSelection.Locate(context.Request.Model.Application)
        .Single(owner => owner.Slice.ReadModels.Any(candidate => candidate.Id == model.Id))) + "." + JavaNames.Type(model.Name);

    internal ProjectionHandlerBinding.ProjectionSequence Projection(IGrouping<SemanticId, SemanticProjectionFrom> group, JavaCodeBuilder code)
    {
        return ForKeys(group.Key, [.. group.Select(from => from.Key)], code);
    }

    internal ProjectionHandlerBinding.ProjectionSequence ForKeys(SemanticId contract, SemanticProjectionKey[] keys, JavaCodeBuilder code) =>
        new(types.EventType(contract, code), UsesModelSequence || keys.Length != 1 ? null : new ProjectionValues(context, types, code).Key(keys[0], Contract(contract)));

    // Reducers write one source-keyed row, but inherit any other writer's model-wide fallback.
    internal ProjectionHandlerBinding.ProjectionSequence Reducer(SemanticEventContract contract, JavaCodeBuilder code) =>
        new(types.EventType(contract.Id, code), UsesModelSequence ? null : new ProjectionValues(context, types, code).Source(contract));

    internal bool NeedsFallback(SemanticProjection projection) => ProjectionTransitions.From(projection)
        .Select(from => (from.EventContract, from.Key))
        .Concat((projection.Scope?.Removals ?? []).Select(removal => (removal.EventContract, removal.Key)))
        .GroupBy(writer => writer.EventContract)
        .Any(group => group.Count() != 1 || new ProjectionValues(context, types).Key(group.Single().Key, Contract(group.Key)) is null);

    SemanticEventContract Contract(SemanticId id) => ScopeSelection.Locate(context.Request.Model.Application)
        .SelectMany(owner => owner.Slice.Events).Single(contract => contract.Id == id);
}
