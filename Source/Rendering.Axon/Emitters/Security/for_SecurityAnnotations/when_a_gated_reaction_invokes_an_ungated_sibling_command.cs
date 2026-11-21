// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_a_gated_reaction_invokes_an_ungated_sibling_command : Specification
{
    ArtifactRenderPlan _baseline;
    ArtifactRenderPlan _plan;

    async Task Establish()
    {
        _baseline = new AxonArtifactRenderPlanner().Plan(await given.availability_models.Request("reaction-constraints"));
        var request = await given.availability_models.Request("reaction-constraints", "slice", "Assign");
        _plan = new AxonArtifactRenderPlanner().Plan(request);
        _baseline.Refusals.ShouldBeEmpty();
        _plan.Refusals.ShouldBeEmpty();
        foreach (var reaction in ScopeSelection.Select(request).Single(located => located.Slice.Name == "Assign").Slice.Reactions)
            Assert.DoesNotContain(given.executable_entries.From(_plan), entry => entry.Sources.Contains(reaction.Id));
    }

    [Fact] void should_have_executable_trigger_ingress_before_gating() => Source(_baseline, "InvokeAliasEndpoint").ShouldContain("runInvokeAlias(input, metadata)");
    [Fact] void should_have_invoking_ingress_on_the_producing_reaction_before_gating() => Source(_baseline, "InvokeProducingAliasEndpoint").ShouldContain("commands.sendAndWait");
    [Fact] void should_have_an_event_runner_before_gating() => Source(_baseline, "AssignAliasReaction").ShouldContain("@EventHandler");
    [Fact] void should_remove_the_invoking_trigger_endpoint() => Assert.DoesNotContain(_plan.Artifacts, artifact => artifact.RelativePath.EndsWith("/InvokeAliasEndpoint.java", StringComparison.Ordinal));
    [Fact] void should_remove_the_producing_reactions_invoking_trigger_endpoint() => Assert.DoesNotContain(_plan.Artifacts, artifact => artifact.RelativePath.EndsWith("/InvokeProducingAliasEndpoint.java", StringComparison.Ordinal));
    [Fact] void should_remove_the_event_runner() => Assert.DoesNotContain(_plan.Artifacts, artifact => artifact.RelativePath.EndsWith("/AssignAliasReaction.java", StringComparison.Ordinal));
    [Fact] void should_leave_the_ungated_sibling_command_unchanged() => Source(_plan, "RequestAliasHandler").ShouldEqual(Source(_baseline, "RequestAliasHandler"));
    [Fact] void should_leave_the_ungated_sibling_reaction_unchanged() => Source(_plan, "ReleaseAliasReaction").ShouldEqual(Source(_baseline, "ReleaseAliasReaction"));

    static string Source(ArtifactRenderPlan plan, string name) => Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
