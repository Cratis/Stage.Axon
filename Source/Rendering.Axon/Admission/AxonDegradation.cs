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
    readonly List<ArtifactRenderDiagnostic> _warnings = [];
    readonly List<string> _notes = [];

    internal bool Strict { get; } = profile.Inputs.Any(input => input.Name == AxonRendering.StrictInput && Encoding.UTF8.GetString(input.Bytes.AsSpan()) == "true");

    internal ImmutableHashSet<string> AllowedApproximations { get; } = AxonApproximationsInput.TryRead(profile, out var allowed) ? allowed : [];

    internal IReadOnlyList<ArtifactRenderDiagnostic> UnsupportedConstructs => _diagnostics;

    internal IReadOnlyList<AxonUnsupportedIntent> Intents => _intents;

    internal IReadOnlyList<string> Notes => _notes;

    /// <summary>Warnings that are never refusals, are not unsupported intents and stay out of the unsupported summary.</summary>
    internal IReadOnlyList<ArtifactRenderDiagnostic> Warnings => _warnings;

    internal ImmutableArray<ArtifactRenderRefusal> Refusals => Strict
        ? [.. _diagnostics.Where(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error).GroupBy(diagnostic => (diagnostic.Artifact, Kind: "construct"))
            .Select(group => new ArtifactRenderRefusal(group.Key.Artifact, group.Key.Kind, [.. group.Select(diagnostic => diagnostic.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]))]
        : [];

    internal void Note(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !_notes.Contains(text, StringComparer.Ordinal)) _notes.Add(text);
    }

    internal bool Approximate(string code, SemanticId construct, string reason, string intent, string modelPath)
    {
        if (!AllowedApproximations.Contains(code))
        {
            Unsupported(code, construct, reason + " Approximation requires opt-in via AxonRendering.AllowApproximations.", intent, modelPath);
            return false;
        }

        _diagnostics.Add(new(code, ArtifactRenderDiagnosticSeverity.Warning, reason, construct));
        _intents.Add(new(code, construct, intent, modelPath));
        return true;
    }

    internal void Warn(string code, SemanticId construct, string reason) => _warnings.Add(new(code, ArtifactRenderDiagnosticSeverity.Warning, reason, construct));

    internal ArtifactRenderDiagnostic Unsupported(string code, SemanticId construct, string reason, string? intent = null, string? modelPath = null)
    {
        var diagnostic = new ArtifactRenderDiagnostic(code, Strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning, reason, construct);
        _diagnostics.Add(diagnostic);
        _intents.Add(new(code, construct, intent ?? reason, modelPath ?? construct.ToString()));
        return diagnostic;
    }
}
