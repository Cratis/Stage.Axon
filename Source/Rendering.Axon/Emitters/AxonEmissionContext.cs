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

    internal void AllocateJava(string package, string name, string? stub = null) =>
        AllocateJava(new(package, name, stub ?? TypeStub(package, name), false, []));

    internal void AddJava(string package, string name, string code, bool test = false, params SemanticId[] sources) =>
        AddJava(new(package, name, TypeStub(package, name), test, sources), code);

    internal string UnsupportedBody(string code, SemanticId owner, string reason)
    {
        Degradation.Unsupported(code, owner, reason);
        return JavaStubs.ThrowingBody(code, reason);
    }

    static string TypeStub(string package, string name) => new JavaCodeBuilder(package).Declare(name)
        .DiffersFromScreenplay("Generation failed; this type is an unsupported stub.")
        .Block($"public class {name}", body => body.Block($"public {name}()", method =>
            method.Line($"throw new {method.Reference("java.lang.UnsupportedOperationException")}(\"Generation failed\");"))).Build();

    void AddJava(JavaAllocation allocation, string code)
    {
        var record = code.IndexOf($"public record {allocation.Name}(", StringComparison.Ordinal);
        var body = record < 0 ? -1 : code.IndexOf('{', record);
        var stub = body < 0 ? allocation.Stub : code[..body] + $"{{ public {allocation.Name} {{ throw new java.lang.UnsupportedOperationException(\"Generation failed\"); }} }}\n";
        AllocateJava(allocation with { Stub = stub });
        Artifacts.Add(PlannedArtifact.CreateText($"src/{(allocation.Test ? "test" : "main")}/java/{allocation.Package.Replace('.', '/')}/{allocation.Name}.java", code, [.. allocation.Sources]));
    }

    void AllocateJava(JavaAllocation allocation)
    {
        var path = $"src/{(allocation.Test ? "test" : "main")}/java/{allocation.Package.Replace('.', '/')}/{allocation.Name}.java";
        AllocatedTypes.TryAdd(path, allocation);
    }

    internal sealed record JavaAllocation(string Package, string Name, string Stub, bool Test, SemanticId[] Sources);
}
