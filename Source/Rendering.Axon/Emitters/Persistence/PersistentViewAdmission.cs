// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewAdmission
{
    internal static bool Context(SemanticProjectionEventContextValue value) => new[] { "eventSourceId", "occurred", "correlationId" }.Contains(value.Path, StringComparer.Ordinal);

    internal static bool Supports(AxonEmissionContext context, SemanticId model)
    {
        var owners = ScopeSelection.Select(context.Request).ToArray();
        var slices = owners.Select(owner => owner.Slice).ToArray();
        var projections = slices.SelectMany(slice => slice.Projections).Where(projection => projection.ReadModel == model).ToArray();
        var readModel = slices.SelectMany(slice => slice.ReadModels).FirstOrDefault(candidate => candidate.Id == model);
        if (readModel is null) return false;
        var reducers = owners.SelectMany(owner => owner.Slice.Reducers.Where(reducer => reducer.ReadModel == model).Select(reducer => (Owner: owner, Reducer: reducer))).ToArray();
        if (projections.Length > 0)
            return reducers.Length == 0 && projections.All(projection => Flat(context, readModel, projection));
        return reducers.Length == 1 && Placed(context, reducers[0].Owner, reducers[0].Reducer);
    }

    internal static bool Placed(AxonEmissionContext context, LocatedSemanticSlice owner, SemanticReducer reducer)
    {
        if (reducer.From is not null || reducer.Transitions.IsEmpty) return false;
        var admission = ReducerBodies.Admission(context, owner, reducer);
        return admission.Blocked is null && admission.Transitions.Values.All(transition => transition.Placed);
    }

    static bool Flat(AxonEmissionContext context, SemanticReadModel model, SemanticProjection projection)
    {
        var transitions = ProjectionTransitions.From(projection).ToArray();
        if (!ProjectionHandlerShape.RootOnly(projection) || transitions.Length == 0) return false;
        if (!ProjectionHandlerShape.Scoped(projection) && transitions.Any(from => from.Mappings.Any(mapping => mapping.Factor is SemanticProjectionEventContextValue))) return false;
        return transitions.All(from => From(context, model, from));
    }

    static bool From(AxonEmissionContext context, SemanticReadModel model, SemanticProjectionFrom from)
    {
        var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).First(candidate => candidate.Id == from.EventContract);
        var application = context.Request.Model.Application;
        var types = new ViewJavaTypes(application, context.BasePackage);
        var values = new ProjectionValues(context, types);
        var records = new ProjectionRecords(application, types);
        return values.Key(from.Key, @event) is not null && from.Mappings.All(mapping => Mapping(values, records, model, @event, mapping));
    }

    static bool Mapping(ProjectionValues values, ProjectionRecords records, SemanticReadModel model, SemanticEventContract @event, SemanticProjectionMapping mapping)
    {
        if (mapping.Source is SemanticProjectionEventContextValue source && !Context(source)) return false;
        if (mapping.Factor is SemanticProjectionEventContextValue factor && !Context(factor)) return false;
        var target = records.Property(model.Properties, mapping.Target);
        if (target is null) return false;
        return values.Mapping(mapping, target, @event) is not null;
    }
}
