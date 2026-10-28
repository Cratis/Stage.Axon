// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter.given;

public abstract class a_planned_screenplay : Specification
{
    private protected ArtifactRenderPlan plan;

    private protected abstract string Model { get; }

    async Task Establish()
    {
        var directory = Directory.CreateTempSubdirectory("reaction-plan");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "model.play"), Model.ReplaceLineEndings("\n"));
            var loaded = await SemanticModelLoader.LoadFromPathAsync(directory.FullName, null, "ReactionPlan");
            plan = AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private protected string Source(string name) => Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());

    private protected bool Exists(string name) => plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal));
}
