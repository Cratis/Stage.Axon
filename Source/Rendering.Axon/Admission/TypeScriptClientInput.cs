// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>The opt-in profile input that asks for the generated TypeScript client.</summary>
internal static class TypeScriptClientInput
{
    internal const string Name = "typescript-client";

    internal static ArtifactRenderInput Create() => ArtifactRenderInput.Create(Name, "1", [.. Encoding.UTF8.GetBytes("true")]);

    /// <summary>Reads the opt-in from a profile.</summary>
    /// <param name="profile">The profile to inspect.</param>
    /// <param name="requested">Whether the client was requested.</param>
    /// <returns>False when the input is present but malformed.</returns>
    internal static bool TryRead(ArtifactRenderProfile profile, out bool requested)
    {
        requested = false;
        var inputs = profile.Inputs.Where(input => input.Name == Name).ToArray();
        if (inputs.Length == 0) return true;
        if (inputs.Length != 1 || inputs[0].Version != "1" || Encoding.UTF8.GetString(inputs[0].Bytes.AsSpan()) != "true") return false;
        requested = true;
        return true;
    }
}
