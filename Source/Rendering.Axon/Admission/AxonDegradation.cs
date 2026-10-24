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
    readonly List<AxonUnsupportedIntent> _intents = [];

    internal bool Strict { get; } = profile.Inputs.Any(input => input.Name == AxonRendering.StrictInput && Encoding.UTF8.GetString(input.Bytes.AsSpan()) == "true");

    internal IReadOnlyList<ArtifactRenderDiagnostic> UnsupportedConstructs => _diagnostics;

    internal IReadOnlyList<AxonUnsupportedIntent> Intents => _intents;

    internal ImmutableArray<ArtifactRenderRefusal> Refusals => Strict
        ? [.. _diagnostics.GroupBy(diagnostic => (diagnostic.Artifact, Kind: "construct"))
            .Select(group => new ArtifactRenderRefusal(group.Key.Artifact, group.Key.Kind, [.. group.Select(diagnostic => diagnostic.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]))]
        : [];

    internal ArtifactRenderDiagnostic Unsupported(string code, SemanticId construct, string reason, string? intent = null, string? modelPath = null)
    {
        var diagnostic = new ArtifactRenderDiagnostic(code, Strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning, reason, construct);
        _diagnostics.Add(diagnostic);
        _intents.Add(new(code, construct, intent ?? reason, modelPath ?? construct.ToString()));
        return diagnostic;
    }
}
