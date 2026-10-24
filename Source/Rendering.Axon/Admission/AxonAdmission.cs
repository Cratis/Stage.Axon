// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Admission;

internal static class AxonAdmission
{
    internal static bool Supports(ExecutableSemanticModel model) => AxonSupportedVersions.Contains(model.LanguageVersion, model.SemanticVersion);

    internal static ImmutableArray<ArtifactRenderDiagnostic> Inspect(ArtifactRenderRequest request, AxonDegradation degradation)
    {
        var diagnostics = ImmutableArray.CreateBuilder<ArtifactRenderDiagnostic>();
        if (!Supports(request.Model))
        {
            degradation.Unsupported(AxonRefusalCodes.Version, request.Model.Application.Id, "Axon scaffolding admits only the measured language/semantic pairs 2.0/2.0, 6.0/6.0, 10.0/10.0 and 25.0/25.0; individual features remain subject to admission.");
        }

        InspectProfile(request, diagnostics);
        InspectScopes(request, diagnostics);
        return diagnostics.ToImmutable();
    }

    static void InspectProfile(ArtifactRenderRequest request, ImmutableArray<ArtifactRenderDiagnostic>.Builder diagnostics)
    {
        var profile = request.Profile;
        if (profile.Target != AxonRendering.Target || profile.TargetVersion != AxonRendering.Version ||
            profile.Renderer != "stage-axon" || profile.RendererVersion != AxonRendering.Version ||
            !ValidInputs(profile))
        {
            diagnostics.Add(Error(AxonRefusalCodes.Profile, "The resolved profile is not a supported Axon scaffold profile.", request));
            return;
        }

        var options = profile.Inputs.Single(input => input.Name == AxonRendering.OptionsInput);
        var modes = Encoding.UTF8.GetString(options.Bytes.AsSpan()).Split(':');
        if (modes.Length != 2)
        {
            diagnostics.Add(Error(AxonRefusalCodes.Profile, "The Axon mode input is malformed.", request));
            return;
        }

        if (modes[0] != "0" && modes[0] != "1")
        {
            diagnostics.Add(Error(AxonRefusalCodes.GapMode, "Axon requires a known implementation-gap policy.", request));
        }

        if (modes[1] != "0" && modes[1] != "1")
        {
            diagnostics.Add(Error(AxonRefusalCodes.RefusalMode, "Axon requires a known refusal policy.", request));
        }
    }

    static bool ValidInputs(ArtifactRenderProfile profile) =>
        profile.Inputs.Count(input => input.Name == AxonRendering.OptionsInput) == 1 &&
        profile.Inputs.Count(input => input.Name == AxonRendering.StrictInput) <= 1 &&
        profile.Inputs.All(input => input.Name == AxonRendering.OptionsInput ? input.Version == "1" : ValidStrictInput(input));

    static bool ValidStrictInput(ArtifactRenderInput input)
    {
        var value = Encoding.UTF8.GetString(input.Bytes.AsSpan());
        return input.Name == AxonRendering.StrictInput && input.Version == "1" && (value == "true" || value == "false");
    }

    static void InspectScopes(ArtifactRenderRequest request, ImmutableArray<ArtifactRenderDiagnostic>.Builder diagnostics)
    {
        var scopes = new[] { request.Scope }.Concat(request.AdditionalScopes).ToArray();
        if (scopes.Any(scope => !ScopeSelection.Exists(request, scope)) ||
            (scopes.Length > 1 && scopes.Any(scope => scope.Kind == ArtifactRenderScopeKind.Application)))
        {
            diagnostics.Add(Error(AxonRefusalCodes.Scope, "Select existing application, module, feature or slice scopes; application scope cannot be combined.", request));
        }
    }

    static ArtifactRenderDiagnostic Error(string code, string message, ArtifactRenderRequest request) =>
        new(code, ArtifactRenderDiagnosticSeverity.Error, message, request.Model.Application.Id);
}
