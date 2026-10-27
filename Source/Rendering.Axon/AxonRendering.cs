// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon;

/// <summary>
/// Plans an experimental Axon Framework application without writing files.
/// Constructs the target cannot express yet degrade to comments, warnings and ledger entries instead of failing the render.
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
    /// <param name="fileHeader">Optional plain-text lines to prefix emitted Java files as comments; none by default.</param>
    /// <remarks>Approximations, such as process-local schedules, are not rendered unless allowed with <see cref="AllowApproximations"/>.</remarks>
    /// <returns>The immutable Axon profile.</returns>
    public static ArtifactRenderProfile CreateProfile(
        ImplementationGapMode implementationGaps = ImplementationGapMode.Refuse,
        ArtifactRefusalMode refusals = ArtifactRefusalMode.Refuse,
        bool strict = false,
        ImmutableArray<string> fileHeader = default)
    {
        ImmutableArray<ArtifactRenderInput> inputs = [ArtifactRenderInput.Create(OptionsInput, "1", [.. Encoding.UTF8.GetBytes($"{(int)implementationGaps}:{(int)refusals}")])];
        if (!fileHeader.IsDefaultOrEmpty) inputs = inputs.Add(FileHeaderInput.Create(fileHeader));
        if (strict) inputs = inputs.Add(ArtifactRenderInput.Create(StrictInput, "1", [.. Encoding.UTF8.GetBytes("true")]));
        return ArtifactRenderProfile.Create(
            Target,
            Version,
            "stage-axon",
            Version,
            inputs);
    }

    /// <summary>Returns the profile with the named approximations explicitly allowed; unlisted approximations stay unsupported.</summary>
    /// <param name="profile">The Axon profile to extend.</param>
    /// <param name="codes">The approximation codes to allow, for example <c>STAGE-AXON-0507</c> for process-local schedules.</param>
    /// <returns>The profile carrying the opt-in.</returns>
    public static ArtifactRenderProfile AllowApproximations(ArtifactRenderProfile profile, params string[] codes) =>
        ArtifactRenderProfile.Create(
            profile.Target,
            profile.TargetVersion,
            profile.Renderer,
            profile.RendererVersion,
            [.. profile.Inputs.Where(input => input.Name != AxonApproximationsInput.Name), AxonApproximationsInput.Create(codes)]);

    /// <summary>Returns the profile with the generated TypeScript client requested; the client is not rendered unless it is requested.</summary>
    /// <param name="profile">The Axon profile to extend.</param>
    /// <returns>The profile carrying the opt-in; applying it again changes nothing.</returns>
    public static ArtifactRenderProfile WithTypeScriptClient(ArtifactRenderProfile profile) =>
        ArtifactRenderProfile.Create(
            profile.Target,
            profile.TargetVersion,
            profile.Renderer,
            profile.RendererVersion,
            [.. profile.Inputs.Where(input => input.Name != TypeScriptClientInput.Name), TypeScriptClientInput.Create()]);

    /// <summary>Returns the profile with bearer-token authentication requested; HTTP Basic remains the default without this input.</summary>
    /// <param name="profile">The Axon profile to extend.</param>
    /// <returns>The profile carrying the opt-in; applying it again changes nothing.</returns>
    public static ArtifactRenderProfile WithBearerTokens(ArtifactRenderProfile profile) =>
        ArtifactRenderProfile.Create(
            profile.Target,
            profile.TargetVersion,
            profile.Renderer,
            profile.RendererVersion,
            [.. profile.Inputs.Where(input => input.Name != HttpAuthenticationInput.Name), HttpAuthenticationInput.Create()]);

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
