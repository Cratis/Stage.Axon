// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>Records unsupported constructs without claiming that they were rendered.</summary>
internal sealed class AxonDegradation(ArtifactRenderProfile profile)
{
    readonly List<ArtifactRenderDiagnostic> _diagnostics = [];

    internal bool Strict { get; } = profile.Inputs.Any(input => input.Name == AxonRendering.StrictInput && Encoding.UTF8.GetString(input.Bytes.AsSpan()) == "true");

    internal IReadOnlyList<ArtifactRenderDiagnostic> UnsupportedConstructs => _diagnostics;

    internal ImmutableArray<ArtifactRenderRefusal> Refusals => Strict
        ? [.. _diagnostics.Select(diagnostic => new ArtifactRenderRefusal(diagnostic.Artifact, "construct", [diagnostic.Code]))]
        : [];

    internal ArtifactRenderDiagnostic Unsupported(string code, SemanticId construct, string reason)
    {
        var diagnostic = new ArtifactRenderDiagnostic(code, Strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning, reason, construct);
        _diagnostics.Add(diagnostic);
        return diagnostic;
    }
}
