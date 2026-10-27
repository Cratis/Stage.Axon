// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter.given;

public class a_client_request : Axon.for_AxonArtifactRenderPlanner.given.an_axon_request
{
    protected ArtifactRenderPlan plan;

    protected ArtifactRenderProfile ClientProfile(bool strict = false) => AxonRendering.WithTypeScriptClient(AxonRendering.CreateProfile(strict: strict));

    protected void Render(Func<SemanticApplication, SemanticApplication>? change = null, bool strict = false)
    {
        var model = loaded.Model;
        var executionPlan = loaded.Plan;
        if (change is not null) model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, change(model.Application) with { CommandReadMode = SemanticCommandReadMode.Protected });
        if (change is not null) executionPlan = SemanticExecutionPlan.Compile(model).Plan!;
        var scope = new ArtifactRenderScope(ArtifactRenderScopeKind.Application, model.Application.Id);
        plan = new AxonArtifactRenderPlanner().Plan(new(model, executionPlan, ClientProfile(strict), scope)
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        });
    }

    protected static SemanticApplication ChangeSlices(SemanticApplication application, Func<SemanticSlice, SemanticSlice> change) => application with
    {
        Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(feature => ChangeFeature(feature, change))] })]
    };

    protected IEnumerable<string> ClientPaths => plan.Artifacts.Select(artifact => artifact.RelativePath).Where(path => path.StartsWith("client/", StringComparison.Ordinal));

    protected string Text(string path) => Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());

    static SemanticFeature ChangeFeature(SemanticFeature feature, Func<SemanticSlice, SemanticSlice> change) => feature with
    {
        Features = [.. feature.Features.Select(child => ChangeFeature(child, change))],
        Slices = [.. feature.Slices.Select(change)]
    };
}
