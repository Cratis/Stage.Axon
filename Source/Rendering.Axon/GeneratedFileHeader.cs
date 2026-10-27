// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>Applies the caller's optional header once to all emitted Java sources, including templates and tests.</summary>
internal static class GeneratedFileHeader
{
    internal static ImmutableArray<PlannedArtifact> Apply(ArtifactRenderProfile profile, IEnumerable<PlannedArtifact> artifacts)
    {
        if (!FileHeaderInput.TryRead(profile, out var lines) || lines.IsEmpty) return [.. artifacts];
        var header = new StringBuilder();
        foreach (var line in lines)
        {
            var text = line.TrimEnd();
            header.Append(text.Length == 0 ? "//" : $"// {text}").Append('\n');
        }
        var comment = header.Append('\n').ToString();

        return [.. artifacts.Select(artifact => Apply(comment, artifact))];
    }

    static PlannedArtifact Apply(string header, PlannedArtifact artifact)
    {
        if (artifact.Kind != PlannedArtifactKind.Text || !artifact.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase)) return artifact;
        return PlannedArtifact.CreateText(artifact.RelativePath, header + Encoding.UTF8.GetString(artifact.Bytes.AsSpan()), artifact.Sources);
    }
}
