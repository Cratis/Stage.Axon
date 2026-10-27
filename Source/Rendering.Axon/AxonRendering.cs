// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Cratis.Screenplay.Semantics;
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

    internal const string OptionsInput = "axon-options";
    internal const string StrictInput = "strict";

    /// <summary>
    /// Gets the package version without build metadata, used for both target and renderer identity.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// Gets the newest ESM semantic version Axon admits. Every semantic version up to this one is admitted.
    /// Unmeasured pairs plan with a STAGE-AXON-VERSION warning, or a refusal in strict mode.
    /// </summary>
    public static SemanticVersion NewestAdmittedSemanticVersion => SemanticVersion.V34;

    /// <summary>
    /// Determines whether the language and semantic version pair has been measured by Axon.
    /// </summary>
    /// <param name="language">The model language version.</param>
    /// <param name="semantic">The model semantic version.</param>
    /// <returns>Whether this exact pair is measured.</returns>
    public static bool IsMeasured(LanguageVersion language, SemanticVersion semantic) => AxonSupportedVersions.Contains(language, semantic);

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
        Plan(
            new AxonRenderRequest(loaded.Model, loaded.Plan, scope)
            {
                AdditionalScopes = additionalScopes.IsDefault ? [] : additionalScopes,
                ImplementationRequirements = loaded.ImplementationRequirements,
                ImplementationContents = loaded.ImplementationContents,
                TypedContextDescriptors = loaded.TypedContextDescriptors,
                AttachmentDiagnostics = loaded.AttachmentDiagnostics,
                ReleaseBaseline = loaded.ReleaseBaseline
            },
            profile ?? CreateProfile());

    /// <summary>
    /// Plans any executable model through the Axon target using typed options and compiler sidecars.
    /// Java package overrides are normalized per segment; segments without ASCII letters or digits are invalid.
    /// Normalized segments must start with a lowercase ASCII letter, contain only lowercase ASCII letters, digits or underscores,
    /// remain canonical under normalization, and not use framework roots. Invalid names produce STAGE-AXON-NAMES.
    /// </summary>
    /// <param name="request">The model, execution plan, scopes, sidecars and options.</param>
    /// <returns>The planned artifacts, diagnostics and refusals without writing files.</returns>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The HTTP authentication mode is undefined.</exception>
    /// <exception cref="InvalidArtifactRenderContract">Header lines or scopes violate the rendering contract.</exception>
    public static ArtifactRenderPlan Plan(AxonRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = request.Options;
        var profile = CreateProfile(strict: options.Strict, fileHeader: options.FileHeader);
        if (!options.AllowedApproximations.IsDefaultOrEmpty) profile = AllowApproximations(profile, [.. options.AllowedApproximations]);
        if (options.TypeScriptClient) profile = WithTypeScriptClient(profile);
        if (!Enum.IsDefined(options.HttpAuthentication)) throw new ArgumentOutOfRangeException(nameof(request), options.HttpAuthentication, "The HTTP authentication mode is undefined.");
        if (options.HttpAuthentication == AxonHttpAuthentication.BearerTokens) profile = WithBearerTokens(profile);
        if (options.JavaPackage is not null || options.ProjectName is not null)
        {
            profile = WithNames(profile, options.JavaPackage is null ? null : AxonNamesInput.Normalize(options.JavaPackage), options.ProjectName);
        }

        return Plan(request, profile);
    }

    internal static ArtifactRenderPlan Plan(AxonRenderRequest request, ArtifactRenderProfile profile) =>
        new AxonArtifactRenderPlanner().Plan(new(request.Model, request.Plan, profile, request.Scope)
        {
            AdditionalScopes = request.AdditionalScopes.IsDefault ? [] : request.AdditionalScopes,
            ImplementationRequirements = request.ImplementationRequirements,
            ImplementationContents = request.ImplementationContents,
            TypedContextDescriptors = request.TypedContextDescriptors,
            AttachmentDiagnostics = request.AttachmentDiagnostics,
            ReleaseBaseline = request.ReleaseBaseline
        });

    internal static ArtifactRenderProfile WithNames(ArtifactRenderProfile profile, string? javaPackage, string? projectName) =>
        ArtifactRenderProfile.Create(
            profile.Target,
            profile.TargetVersion,
            profile.Renderer,
            profile.RendererVersion,
            [.. profile.Inputs.Where(input => input.Name != AxonNamesInput.Name), AxonNamesInput.Create(javaPackage, projectName)]);

    static string ReadVersion()
    {
        var version = typeof(AxonRendering).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
        return string.IsNullOrEmpty(version) ? "0.0.0-local" : version;
    }
}
