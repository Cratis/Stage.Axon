// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Admission;

/// <summary>The opt-in profile input that selects bearer-token authentication instead of HTTP Basic.</summary>
internal static class HttpAuthenticationInput
{
    internal const string Name = "http-authentication";

    internal static ArtifactRenderInput Create() => ArtifactRenderInput.Create(Name, "1", [.. Encoding.UTF8.GetBytes("bearer")]);

    /// <summary>Reads the authentication selection from a profile.</summary>
    /// <param name="profile">The profile to inspect.</param>
    /// <param name="bearer">Whether bearer tokens were requested.</param>
    /// <returns>False when the input is present but malformed.</returns>
    internal static bool TryRead(ArtifactRenderProfile profile, out bool bearer)
    {
        bearer = false;
        var inputs = profile.Inputs.Where(input => input.Name == Name).ToArray();
        if (inputs.Length == 0) return true;
        if (inputs.Length != 1) return false;
        if (inputs[0].Version != "1") return false;
        if (Encoding.UTF8.GetString(inputs[0].Bytes.AsSpan()) != "bearer") return false;
        bearer = true;
        return true;
    }

    internal static bool Bearer(ArtifactRenderProfile profile) => TryRead(profile, out var bearer) && bearer;
}
