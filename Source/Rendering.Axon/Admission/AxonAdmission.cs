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
    // Measured from the authored orders, ticketing and inventory blueprints with Screenplay 4.240.2.
    internal static bool Supports(ExecutableSemanticModel model) =>
        (model.LanguageVersion.Major, model.LanguageVersion.Minor, model.SemanticVersion.Major, model.SemanticVersion.Minor) == (2, 0, 2, 0);

    internal static ImmutableArray<ArtifactRenderDiagnostic> Inspect(ArtifactRenderRequest request)
    {
        var diagnostics = ImmutableArray.CreateBuilder<ArtifactRenderDiagnostic>();
        if (!Supports(request.Model))
        {
            diagnostics.Add(Error(AxonRefusalCodes.Version, "Axon scaffolding admits only the measured language/semantic pair 2.0/2.0.", request));
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
            profile.Inputs.Length != 1 || profile.Inputs[0].Name != AxonRendering.OptionsInput || profile.Inputs[0].Version != "1")
        {
            diagnostics.Add(Error(AxonRefusalCodes.Profile, "The resolved profile is not a supported Axon scaffold profile.", request));
            return;
        }

        var modes = Encoding.UTF8.GetString(profile.Inputs[0].Bytes.AsSpan()).Split(':');
        if (modes.Length != 2)
        {
            diagnostics.Add(Error(AxonRefusalCodes.Profile, "The Axon mode input is malformed.", request));
            return;
        }

        if (modes[0] != "0")
        {
            diagnostics.Add(Error(AxonRefusalCodes.GapMode, "Axon does not support implementation-gap stubs; use Refuse.", request));
        }

        if (modes[1] != "0")
        {
            diagnostics.Add(Error(AxonRefusalCodes.RefusalMode, "Axon does not support render-around plans; use Refuse.", request));
        }
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
