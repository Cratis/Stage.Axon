// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewAdmission
{
    internal static bool Context(SemanticProjectionEventContextValue value) => new[] { "eventSourceId", "occurred", "correlationId" }.Contains(value.Path, StringComparer.Ordinal);

    internal static bool Supports(AxonEmissionContext context, SemanticId model) => RefusalReason(context, model) is null;

    internal static bool Writerless(AxonEmissionContext context, SemanticId model)
    {
        var (projections, reducers) = Writers(context, model);
        return projections.Length == 0 && reducers.Length == 0;
    }

    internal static bool HasQueryPerformer(AxonEmissionContext context, SemanticId model) =>
        ScopeSelection.Select(context.Request)
            .SelectMany(owner => owner.Slice.Queries.Concat(QueryPerformer.Queries(context, owner)))
            .Any(query => query.ReadModel == model && QueryPerformer.Required(context, query) &&
                QueryPerformer.Requirement(context, query)?.QueryShape?.Arguments.Any(argument => argument.Type is null) != true);

    internal static string? RefusalReason(AxonEmissionContext context, SemanticId model)
    {
        var (projections, reducers) = Writers(context, model);
        var readModel = ScopeSelection.Select(context.Request).SelectMany(owner => owner.Slice.ReadModels).FirstOrDefault(candidate => candidate.Id == model);
        if (readModel is null) return "its shape is not admitted for persistence";
        var admitted = projections.Length > 0
            ? reducers.Length == 0 && projections.All(projection => Flat(context, readModel, projection))
            : reducers.Length == 1 && Placed(context, reducers[0].Owner, reducers[0].Reducer);
        if (admitted) return null;
        var reasons = new List<string>();
        reasons.AddRange(WriterReasons(projections.Length, reducers.Length));
        if (reducers.Any(reducer => !Placed(context, reducer.Owner, reducer.Reducer))) reasons.Add("its reducer is not fully placed (unauthored transitions or an event filter)");
        foreach (var projection in projections) reasons.AddRange(ProjectionReasons(context, readModel, projection));
        return reasons.Count == 0 ? "its shape is not admitted for persistence" : string.Join("; ", reasons);
    }

    internal static bool Placed(AxonEmissionContext context, LocatedSemanticSlice owner, SemanticReducer reducer)
    {
        if (reducer.From is not null || reducer.Transitions.IsEmpty) return false;
        var admission = ReducerBodies.Admission(context, owner, reducer);
        return admission.Blocked is null && admission.Transitions.Values.All(transition => transition.Placed);
    }

    static (SemanticProjection[] Projections, (LocatedSemanticSlice Owner, SemanticReducer Reducer)[] Reducers) Writers(AxonEmissionContext context, SemanticId model)
    {
        var owners = ScopeSelection.Select(context.Request);
        return (
            owners.SelectMany(owner => owner.Slice.Projections).Where(projection => projection.ReadModel == model).ToArray(),
            owners.SelectMany(owner => owner.Slice.Reducers.Where(reducer => reducer.ReadModel == model).Select(reducer => (Owner: owner, Reducer: reducer))).ToArray());
    }

    static IEnumerable<string> WriterReasons(int projections, int reducers)
    {
        if (projections == 0 && reducers == 0) yield return "it has no projection or reducer that writes it";
        if (projections > 0 && reducers > 0) yield return "it has both a projection and a reducer writer";
        if (reducers > 1) yield return "it has more than one reducer writer";
    }

    static bool Stateful(SemanticProjectionScope scope) => scope.Every is not null ||
        new[] { scope.Children.Length, scope.Nested.Length, scope.Joins.Length }.Any(count => count > 0);

    static bool UnsupportedMappings(AxonEmissionContext context, SemanticReadModel model, SemanticProjection projection) =>
        UnsupportedFactor(projection) || ProjectionTransitions.From(projection).Any(from => !From(context, model, from));

    static bool UnsupportedFactor(SemanticProjection projection) => !ProjectionHandlerShape.Scoped(projection) &&
        ProjectionTransitions.From(projection).Any(from => from.Mappings.Any(mapping => mapping.Factor is SemanticProjectionEventContextValue));

    static IEnumerable<string> ProjectionReasons(AxonEmissionContext context, SemanticReadModel model, SemanticProjection projection)
    {
        var transitions = ProjectionTransitions.From(projection).ToArray();
        if (projection.Scope is { } scope)
        {
            if (Stateful(scope))
                yield return "its projection uses a stateful scope (every, children, nested scopes or joins)";
            if (scope.From.Any(from => from.Key is not SemanticProjectionValueKey))
                yield return "its projection uses a composite root key";
            if (scope.Removals.Length > 0 || scope.JoinRemovals.Length > 0)
                yield return "its projection removes rows";
        }
        if (transitions.Length == 0) yield return "its projection has no event transitions";
        if (UnsupportedMappings(context, model, projection))
            yield return "its projection maps an unsupported event-context value, target or expression";
    }

    static bool Flat(AxonEmissionContext context, SemanticReadModel model, SemanticProjection projection)
    {
        var transitions = ProjectionTransitions.From(projection).ToArray();
        if (!ProjectionHandlerShape.RootOnly(projection) || transitions.Length == 0) return false;
        if (UnsupportedFactor(projection)) return false;
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
