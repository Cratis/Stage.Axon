// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Plans an experimental, scaffold-only Axon Framework application without writing files.
/// Domain handlers, projections, policies and specifications are not emitted in this phase.
/// </summary>
public static class AxonRendering
{
    /// <summary>Gets the target identity.</summary>
    public const string Target = "axon";

    /// <summary>Gets the exact experimental target and renderer version.</summary>
    public const string Version = "0.1.0";

    internal const string OptionsInput = "axon-options";
    internal const string StrictInput = "strict";

    /// <summary>Creates a fully resolved profile. Unsupported constructs warn unless strict mode is enabled.</summary>
    /// <param name="implementationGaps">The requested implementation-gap policy.</param>
    /// <param name="refusals">The requested artifact-refusal policy.</param>
    /// <param name="strict">Whether unsupported constructs produce errors and refusals instead of warnings.</param>
    /// <returns>The immutable Axon profile.</returns>
    public static ArtifactRenderProfile CreateProfile(
        ImplementationGapMode implementationGaps = ImplementationGapMode.Refuse,
        ArtifactRefusalMode refusals = ArtifactRefusalMode.Refuse,
        bool strict = false)
    {
        ImmutableArray<ArtifactRenderInput> inputs = [ArtifactRenderInput.Create(OptionsInput, "1", [.. Encoding.UTF8.GetBytes($"{(int)implementationGaps}:{(int)refusals}")])];
        if (strict) inputs = inputs.Add(ArtifactRenderInput.Create(StrictInput, "1", [.. Encoding.UTF8.GetBytes("true")]));
        return ArtifactRenderProfile.Create(
            Target,
            Version,
            "stage-axon",
            Version,
            inputs);
    }

    /// <summary>Plans the selected scope from a compiler result, preserving its implementation sidecars.</summary>
    /// <param name="loaded">The compiled semantic model.</param>
    /// <param name="scope">The primary selected scope.</param>
    /// <param name="additionalScopes">Additional scopes to union with the primary scope.</param>
    /// <param name="profile">An optional resolved Axon profile.</param>
    /// <returns>The plan with explicit warnings for unsupported constructs, or errors for invalid requests and strict refusals.</returns>
    public static ArtifactRenderPlan PlanFrom(
        LoadedSemanticModel loaded,
        ArtifactRenderScope scope,
        ImmutableArray<ArtifactRenderScope> additionalScopes = default,
        ArtifactRenderProfile? profile = null) =>
        new AxonArtifactRenderPlanner().Plan(new(loaded.Model, loaded.Plan, profile ?? CreateProfile(), scope)
        {
            AdditionalScopes = additionalScopes.IsDefault ? [] : additionalScopes,
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents,
            TypedContextDescriptors = loaded.TypedContextDescriptors,
            AttachmentDiagnostics = loaded.AttachmentDiagnostics,
            ReleaseBaseline = loaded.ReleaseBaseline
        });
}
