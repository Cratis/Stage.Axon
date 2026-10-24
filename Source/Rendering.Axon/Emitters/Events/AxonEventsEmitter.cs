// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events;

internal sealed class AxonEventsEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var selected = ScopeSelection.Select(context.Request).Select(slice => slice.Module.Id).ToHashSet();
        var applicationScope = context.Request.Scope.Kind == ArtifactRenderScopeKind.Application;
        var events = ScopeSelection.Locate(context.Request.Model.Application)
            .Where(slice => applicationScope || selected.Contains(slice.Module.Id) || slice.Module.Id == context.Request.Scope.Artifact)
            .SelectMany(slice => slice.Slice.Events.Select(@event => (slice.Module, Event: @event))).DistinctBy(pair => pair.Event.Id);
        foreach (var (module, contract) in events) Emit(context, module, contract);
    }

    internal static string Package(AxonEmissionContext context, SemanticModule module) => context.BasePackage + "." + JavaNames.PackageSegment(module.Name) + ".events";

    internal static string TypeFor(AxonEmissionContext context, SemanticId contract) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events.Select(@event => (slice.Module, Event: @event)))
            .Where(pair => pair.Event.Id == contract).DistinctBy(pair => pair.Event.Id)
            .Select(pair => Package(context, pair.Module) + "." + JavaNames.Type(pair.Event.Name)).Single();

    static void Emit(AxonEmissionContext context, SemanticModule module, SemanticEventContract contract)
    {
        var annotation = "@org.axonframework.messaging.eventhandling.annotation.Event(namespace = " + JavaCodeBuilder.StringLiteral(JavaNames.PackageSegment(module.Name)) + ", name = " + JavaCodeBuilder.StringLiteral(contract.StoredName ?? contract.Name) + ", version = " + JavaCodeBuilder.StringLiteral(contract.Revision.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)) + ")";
        if (!contract.PriorRevisions.IsDefaultOrEmpty || contract.Predecessor is not null)
        {
            const string intent = "upcast historical event revisions";
            var path = contract.Id + "/priorRevisions";
            context.Degradation.Unsupported(AxonRefusalCodes.Evolution, contract.Id, "Historical event revisions require an Axon upcaster implementation.", intent, path);
            annotation = JavaCodeBuilder.UnsupportedComment(intent, path, AxonRefusalCodes.Evolution) + "\n" + annotation;
        }
        var name = JavaNames.Type(contract.Name);
        context.AddJava(Package(context, module), name, AxonEventSources.Record(context, Package(context, module), contract, annotation), sources: [contract.Id]);
    }
}
