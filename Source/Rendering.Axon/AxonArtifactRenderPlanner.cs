// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Plans the experimental Axon target, preserving explicit diagnostics for unsupported modeled behavior.
/// </summary>
public sealed class AxonArtifactRenderPlanner : IArtifactRenderPlanner
{
    readonly IReadOnlyList<IAxonEmitter> _emitters;

    /// <summary>Creates a planner with the supported emitter families.</summary>
    public AxonArtifactRenderPlanner() : this(AxonEmitters.Ordered)
    {
    }

    internal AxonArtifactRenderPlanner(IReadOnlyList<IAxonEmitter> emitters) => _emitters = emitters;

    /// <inheritdoc/>
    /// <exception cref="InvalidArtifactRenderContract">The request does not satisfy the artifact render contract, including scope selection.</exception>
    public ArtifactRenderPlan Plan(ArtifactRenderRequest request)
    {
        var degradation = new AxonDegradation(request.Profile);
        var diagnostics = AxonAdmission.Inspect(request, degradation);
        var context = new AxonEmissionContext(request, degradation);
        if (diagnostics.IsEmpty && degradation.Refusals.IsEmpty)
        {
            InspectNames(context);
            Emit(context, _emitters);
            GuardPaths(context);
            AppendFallbackNotes(context);
            UnsupportedSummary.Emit(context);
        }

        return ArtifactRenderPlan.Create(
            request,
            degradation.Refusals.IsEmpty ? GeneratedFileHeader.Apply(request.Profile, context.Artifacts) : [],
            [.. diagnostics, .. degradation.UnsupportedConstructs, .. degradation.Warnings],
            [],
            degradation.Refusals,
            []);
    }

    static void Emit(AxonEmissionContext context, IReadOnlyList<IAxonEmitter> emitters)
    {
        foreach (var emitter in emitters.Where(emitter => emitter.IsImplemented)) EmitSafely(context, emitter);
        InlineImplementationDiagnostics.Emit(context);
        if (context.Artifacts.Count == 0)
        {
            context.Degradation.Unsupported(
                AxonRefusalCodes.Scaffold,
                context.Request.Model.Application.Id,
                "Axon scaffold emission is unavailable until the Phase 0 runtime has been verified.");
        }
    }

    static void EmitSafely(AxonEmissionContext context, IAxonEmitter emitter)
    {
        var allocated = context.AllocatedTypes.Keys.ToHashSet(StringComparer.Ordinal);
        var consumed = context.ConsumedImplementations.ToArray();
        try
        {
            emitter.Emit(context);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            context.ConsumedImplementations.Clear();
            context.ConsumedImplementations.UnionWith(consumed);
            var completed = context.Artifacts.Select(artifact => artifact.RelativePath).ToHashSet(StringComparer.Ordinal);
            foreach (var allocation in context.AllocatedTypes.Where(pair => !allocated.Contains(pair.Key) && !completed.Contains(pair.Key)).Select(pair => pair.Value).ToArray())
            {
                context.AddJava(allocation.Package, allocation.Name, allocation.Stub, allocation.Test, allocation.Sources);
            }
            context.Degradation.Unsupported(AxonRefusalCodes.Generation, context.Request.Model.Application.Id, $"Emitter '{emitter.GetType().Name}' failed ({exception.GetType().Name}); completed artifacts are retained and incomplete allocations use explicit API-preserving stubs.");
        }
    }

    static void GuardPaths(AxonEmissionContext context)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<PlannedArtifact>();
        foreach (var artifact in context.Artifacts)
        {
            if (paths.Add(artifact.RelativePath))
            {
                kept.Add(artifact);
            }
            else
            {
                context.Degradation.Unsupported(AxonRefusalCodes.Generation, context.Request.Model.Application.Id, $"Dropped duplicate or case-colliding artifact '{artifact.RelativePath}'; the first artifact is kept. References may not compile until name propagation is implemented.");
            }
        }
        context.Artifacts.Clear();
        context.Artifacts.AddRange(kept);
    }

    static void InspectNames(AxonEmissionContext context)
    {
        var application = context.Request.Model.Application;
        AxonNamesInput.BasePackage(context.Request, (requested, prefixed) =>
            context.Degradation.Warn(AxonRefusalCodes.Names, application.Id, $"JDK-root base package '{requested}' was prefixed to '{prefixed}'."));
        var properties = application.Types.SelectMany(type => type.Properties).Concat(ScopeSelection.Locate(application).SelectMany(located =>
            located.Slice.Commands.SelectMany(command => command.Properties)
                .Concat(located.Slice.Events.SelectMany(contract => contract.Properties))
                .Concat(located.Slice.ReadModels.SelectMany(model => model.Properties))));
        foreach (var property in properties.DistinctBy(property => property.Id).Where(property => JavaNames.RenamesObjectMember(property.Name)))
        {
            context.Degradation.Warn(AxonRefusalCodes.Names, property.Id, $"Reserved Object member '{property.Name}' was renamed to '{JavaNames.Member(property.Name)}'; the JSON wire name is unchanged.");
        }
    }

    static void AppendFallbackNotes(AxonEmissionContext context)
    {
        foreach (var diagnostic in context.Degradation.Warnings.Where(diagnostic => diagnostic.Code == AxonRefusalCodes.Names))
        {
            context.Degradation.Note($"{diagnostic.Code}: {diagnostic.Message}");
        }
        if (context.Request.ImplementationContents.Count > 0)
        {
            context.Degradation.Note("Inline Java bodies are emitted verbatim. Hard-coded package names may no longer match generated names after normalization or renaming; bodies are not rewritten.");
        }
    }
}
