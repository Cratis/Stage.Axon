// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters;

internal sealed class AxonEmissionContext(ArtifactRenderRequest request, AxonDegradation degradation)
{
    internal ArtifactRenderRequest Request { get; } = request;

    internal AxonDegradation Degradation { get; } = degradation;

    internal string BasePackage { get; } = AxonNamesInput.BasePackage(request);

    internal string ArtifactId => AxonNamesInput.ArtifactId(Request, BasePackage);

    internal List<PlannedArtifact> Artifacts { get; } = [];

    internal HashSet<string> ConsumedImplementations { get; } = new(StringComparer.Ordinal);

    internal Dictionary<SemanticId, AxonSpecificationEntry> Specifications { get; } = [];

    internal Dictionary<SemanticId, AxonHttpScenario> HttpScenarios { get; } = [];

    internal IReadOnlyDictionary<string, SemanticSurfaceDisposition> SemanticSurface => SemanticSurfaceLedger.Entries;

    internal Dictionary<string, JavaAllocation> AllocatedTypes { get; } = new(StringComparer.Ordinal);

    // Incomplete allocations must supply their downstream API; it cannot be reconstructed from a type name.
    internal void AllocateJava(string package, string name, string stub) =>
        AllocateJava(new(package, name, stub, false, []));

    internal void AddJava(string package, string name, string code, bool test = false, params SemanticId[] sources) =>
        AddJava(new(package, name, code, test, sources), code);

    internal string UnsupportedBody(string code, SemanticId owner, string reason)
    {
        Degradation.Unsupported(code, owner, reason);
        return JavaStubs.ThrowingBody(code, reason);
    }

    void AddJava(JavaAllocation allocation, string code)
    {
        AllocateJava(allocation);
        Artifacts.Add(PlannedArtifact.CreateText($"src/{(allocation.Test ? "test" : "main")}/java/{allocation.Package.Replace('.', '/')}/{allocation.Name}.java", code, [.. allocation.Sources]));
    }

    void AllocateJava(JavaAllocation allocation)
    {
        var path = $"src/{(allocation.Test ? "test" : "main")}/java/{allocation.Package.Replace('.', '/')}/{allocation.Name}.java";
        AllocatedTypes.TryAdd(path, allocation);
    }

    internal sealed record JavaAllocation(string Package, string Name, string Stub, bool Test, SemanticId[] Sources);
}
