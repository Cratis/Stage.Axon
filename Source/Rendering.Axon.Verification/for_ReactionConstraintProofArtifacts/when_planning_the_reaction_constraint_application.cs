// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ReactionConstraintProofArtifacts;

public class when_planning_the_reaction_constraint_application : Specification
{
    ArtifactRenderPlan _plan;

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        var compilation = await SemanticModelLoader.LoadAsync(Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus"), ["reaction-constraints.play"], null, "CorpusReactionConstraints");
        if (!compilation.Success) throw new NativeVerificationFailed(string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        _plan = AxonRendering.PlanFrom(compilation.Loaded, new(ArtifactRenderScopeKind.Application, compilation.Loaded.Model.Application.Id), default, CorpusRenderer.Profile);
    }

    [Fact] void should_plan_without_refusals() => (_plan.Success && _plan.Refusals.IsEmpty).ShouldBeTrue();
    [Fact] void should_apply_the_constraint_on_assignment() => Source("AssignAliasOn0AppendHandler").ShouldContain("ModeledConstraints.apply(uniqueValues, command.event(), processingContext);");
    [Fact] void should_apply_the_constraint_on_release() => Source("ReleaseAliasOn0AppendHandler").ShouldContain("ModeledConstraints.apply(uniqueValues, command.event(), processingContext);");
    [Fact] void should_assign_the_unique_alias() => Source("ModeledConstraints").ShouldContain("store.assign(context,");
    [Fact] void should_release_the_owner() => Source("ModeledConstraints").ShouldContain("store.releaseOwner(context,");
    [Fact] void should_disallow_replaying_the_reaction() => Source("AssignAliasReaction").ShouldContain("@DisallowReplay");

    string Source(string name) => Encoding.UTF8.GetString(_plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
