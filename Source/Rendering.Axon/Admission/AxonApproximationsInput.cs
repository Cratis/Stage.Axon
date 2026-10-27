// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static class AxonApproximationsInput
{
    internal const string Name = "axon-approximations";

    internal static ArtifactRenderInput Create(IEnumerable<string> codes)
    {
        var sorted = codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return ArtifactRenderInput.Create(Name, "1", [.. Encoding.UTF8.GetBytes(string.Join(',', sorted))]);
    }

    internal static bool TryRead(ArtifactRenderProfile profile, out ImmutableHashSet<string> codes)
    {
        codes = [];
        var inputs = profile.Inputs.Where(input => input.Name == Name).ToArray();
        if (inputs.Length == 0) return true;
        if (inputs.Length != 1 || inputs[0].Version != "1") return false;

        var text = Encoding.UTF8.GetString(inputs[0].Bytes.AsSpan());
        var read = text.Length == 0 ? [] : text.Split(',');
        if (read.Any(code => !AxonRefusalCodes.Approximations.Contains(code))) return false;
        if (!Create(read).Bytes.SequenceEqual(inputs[0].Bytes)) return false;
        codes = [.. read];
        return true;
    }
}
