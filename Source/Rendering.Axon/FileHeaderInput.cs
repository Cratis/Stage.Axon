// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon;

internal static class FileHeaderInput
{
    internal const string Name = "axon-render:file-header";

    internal static ArtifactRenderInput Create(ImmutableArray<string> lines)
    {
        if (lines.IsDefaultOrEmpty || !ValidLines(lines))
        {
            throw new InvalidArtifactRenderContract("A file header input requires one or more single-line, non-null lines.");
        }

        return ArtifactRenderInput.Create(Name, "1", [.. JsonSerializer.SerializeToUtf8Bytes(lines.ToArray())]);
    }

    internal static bool TryRead(ArtifactRenderProfile profile, out ImmutableArray<string> lines)
    {
        lines = [];
        var inputs = profile.Inputs.Where(input => input.Name == Name).ToArray();
        if (inputs.Length == 0) return true;
        if (inputs.Length != 1 || inputs[0].Version != "1") return false;

        try
        {
            var read = JsonSerializer.Deserialize<string[]>(new UTF8Encoding(false, true).GetString(inputs[0].Bytes.AsSpan()));
            if (read is null || read.Length == 0 || !ValidLines(read)) return false;
            var canonical = Create([.. read]);
            if (!canonical.Bytes.SequenceEqual(inputs[0].Bytes)) return false;
            lines = [.. read];
            return true;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    static bool ValidLines(IEnumerable<string> lines) =>
        lines.All(line => line?.Contains('\n') == false && !line.Contains('\r'));
}
