// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission.given;

public class a_model_with_behaviors : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    private protected AxonEmissionContext context;
    private protected LanguageVersion languageVersion = LanguageVersion.V9;
    private protected SemanticVersion semanticVersion = SemanticVersion.V9;
    private protected SemanticId changedSlice;
    private protected SemanticId otherSlice;

    private protected SemanticSlice Seed() => ScopeSelection.Select(request)[0].Slice with { Captures = [], Events = [], Reactions = [] };

    private protected void Change(Func<SemanticSlice, SemanticSlice> change)
    {
        var done = false;
        var application = request.Model.Application;
        SemanticSlice Apply(SemanticSlice slice)
        {
            if (done || slice.Events.IsDefaultOrEmpty || slice.Commands.IsDefaultOrEmpty) return slice;

            done = true;
            changedSlice = slice.Id;
            return change(slice);
        }

        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(Apply)] };
        Replace(application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        otherSlice = ScopeSelection.Locate(request.Model.Application).First(located => located.Slice.Id != changedSlice).Slice.Id;
    }

    private protected void Replace(SemanticApplication application) =>
        request = request with { Model = ExecutableSemanticModel.Create(languageVersion, semanticVersion, application) };

    private protected void Emit()
    {
        context = new(request, new(request.Profile));
        new AxonBehaviorAdmission().Emit(context);
    }

    private protected IEnumerable<string> PackageInfo() => context.Artifacts
        .Where(artifact => artifact.RelativePath.EndsWith("/package-info.java", StringComparison.Ordinal))
        .Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()));
}
