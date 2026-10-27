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
        var slices = ScopeSelection.Select(context.Request).Select(owner => owner.Slice).ToArray();
        var projections = slices.SelectMany(slice => slice.Projections).Where(projection => projection.ReadModel == model).ToArray();
        var readModel = slices.SelectMany(slice => slice.ReadModels).FirstOrDefault(candidate => candidate.Id == model);
        if (readModel is null || projections.Length == 0) return false;
        return projections.All(projection => Flat(context, readModel, projection)) &&
            !slices.SelectMany(slice => slice.Reducers).Any(reducer => reducer.ReadModel == model);
    }

    static bool Flat(AxonEmissionContext context, SemanticReadModel model, SemanticProjection projection)
    {
        if (!ProjectionHandlerShape.RootOnly(projection) || !ProjectionTransitions.From(projection).Any()) return false;
        return ProjectionTransitions.From(projection).All(from => From(context, model, from));
    }

    static bool From(AxonEmissionContext context, SemanticReadModel model, SemanticProjectionFrom from)
    {
        var @event = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(owner => owner.Slice.Events).First(candidate => candidate.Id == from.EventContract);
        var application = context.Request.Model.Application;
        var types = new ViewJavaTypes(application);
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
