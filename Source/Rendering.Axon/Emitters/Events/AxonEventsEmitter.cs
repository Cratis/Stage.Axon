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
        var contracts = Contracts(context).ToList();
        foreach (var (module, contract) in contracts) Emit(context, module, contract);
        if (contracts.Exists(pair => AxonEventSources.PrimaryTag(context, pair.Event).JavaType == AxonEventSources.FallbackType(context.BasePackage))) EmitFallbackIdentity(context);
    }

    internal static IEnumerable<(SemanticModule Module, SemanticEventContract Event)> Contracts(AxonEmissionContext context)
    {
        var selected = ScopeSelection.Select(context.Request).Select(slice => slice.Module.Id).ToHashSet();
        var applicationScope = context.Request.Scope.Kind == ArtifactRenderScopeKind.Application;
        return ScopeSelection.Locate(context.Request.Model.Application)
            .Where(slice => applicationScope || selected.Contains(slice.Module.Id) || slice.Module.Id == context.Request.Scope.Artifact)
            .SelectMany(slice => slice.Slice.Events.Select(@event => (slice.Module, Event: @event))).DistinctBy(pair => pair.Event.Id);
    }

    internal static string Package(AxonEmissionContext context, SemanticModule module) => context.BasePackage + "." + JavaNames.PackageSegment(module.Name) + ".events";

    internal static string TypeFor(AxonEmissionContext context, SemanticId contract) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events.Select(@event => (slice.Module, Event: @event)))
            .Where(pair => pair.Event.Id == contract).DistinctBy(pair => pair.Event.Id)
            .Select(pair => Package(context, pair.Module) + "." + JavaNames.Type(pair.Event.Name)).Single();

    internal static string TypeFor(AxonEmissionContext context, SemanticId contract, JavaCodeBuilder code, string package)
    {
        var type = TypeFor(context, contract);
        var separator = type.LastIndexOf('.');
        var name = type[(separator + 1)..];
        if (type[..separator] == package) code.Declare(name);
        else return code.Reference(type);
        return name;
    }

    static void EmitFallbackIdentity(AxonEmissionContext context)
    {
        var package = context.BasePackage + ".events";
        var code = new JavaCodeBuilder(package).Declare("EventSourceId");
        var value = "@" + code.Reference("com.fasterxml.jackson.annotation.JsonValue");
        var creator = "@" + code.Reference("com.fasterxml.jackson.annotation.JsonCreator") + "(mode = JsonCreator.Mode.DELEGATING)";
        code.Block($"public record EventSourceId({value} {code.Reference("java.lang.String")} value)", body =>
        {
            body.Line(creator).Block("public EventSourceId", constructor => constructor.Line($"{body.Reference("java.util.Objects")}.requireNonNull(value, \"value\");"))
                .Line("@" + body.Reference("java.lang.Override")).Block($"public {body.Reference("java.lang.String")} toString()", method => method.Line("return value;"));
        });
        context.AddJava(package, "EventSourceId", code.Build(), sources: [context.Request.Model.Application.Id]);
    }

    static void Emit(AxonEmissionContext context, SemanticModule module, SemanticEventContract contract)
    {
        var comment = string.Empty;
        if (!contract.PriorRevisions.IsDefaultOrEmpty || contract.Predecessor is not null)
        {
            const string intent = "upcast historical event revisions";
            var path = contract.Id + "/priorRevisions";
            context.Degradation.Unsupported(AxonRefusalCodes.Evolution, contract.Id, "Historical event revisions require an Axon upcaster implementation.", intent, path);
            comment = JavaCodeBuilder.UnsupportedComment(intent, path, AxonRefusalCodes.Evolution) + "\n";
        }
        var name = JavaNames.Type(contract.Name);
        string Annotation(JavaCodeBuilder code) => comment + "@" + code.Reference("org.axonframework.messaging.eventhandling.annotation.Event") + "(namespace = " + JavaCodeBuilder.StringLiteral(JavaNames.PackageSegment(module.Name)) + ", name = " + JavaCodeBuilder.StringLiteral(contract.StoredName ?? contract.Name) + ", version = " + JavaCodeBuilder.StringLiteral(contract.Revision.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)) + ")";
        context.AddJava(Package(context, module), name, AxonEventSources.Record(context, Package(context, module), contract, Annotation), sources: [contract.Id]);
    }
}
