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

    internal string BasePackage => JavaNames.PackageSegment(Request.Model.Application.Name);

    internal List<PlannedArtifact> Artifacts { get; } = [];

    internal Dictionary<SemanticId, AxonSpecificationEntry> Specifications { get; } = [];

    internal IReadOnlyDictionary<string, SemanticSurfaceDisposition> SemanticSurface => SemanticSurfaceLedger.Entries;

    internal void AddJava(string package, string name, string code, bool test = false, params SemanticId[] sources) =>
        Artifacts.Add(PlannedArtifact.CreateText($"src/{(test ? "test" : "main")}/java/{package.Replace('.', '/')}/{name}.java", code, [.. sources]));

    internal string UnsupportedBody(string code, SemanticId owner, string reason)
    {
        Degradation.Unsupported(code, owner, reason);
        return JavaStubs.ThrowingBody(code, reason);
    }
}
