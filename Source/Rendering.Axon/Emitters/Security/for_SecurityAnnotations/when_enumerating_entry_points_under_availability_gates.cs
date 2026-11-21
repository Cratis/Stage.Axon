// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_enumerating_entry_points_under_availability_gates : Specification
{
    [Theory]
    [InlineData("reaction-constraints", "module")]
    [InlineData("reaction-constraints", "feature")]
    [InlineData("reaction-constraints", "slice")]
    [InlineData("inline-java-policy-performers", "module")]
    [InlineData("inline-java-policy-performers", "feature")]
    [InlineData("inline-java-policy-performers", "slice")]
    public async Task should_deny_or_omit_every_executable_entry_point_owned_by_a_gated_operation(string sample, string owner)
    {
        var baseline = new AxonArtifactRenderPlanner().Plan(await given.availability_models.Request(sample));
        var request = await given.availability_models.Request(sample, owner);
        var plan = new AxonArtifactRenderPlanner().Plan(request);
        baseline.Refusals.ShouldBeEmpty();
        plan.Refusals.ShouldBeEmpty();

        // Derive owners independently of AvailabilityGates, and discover entry points from Java, not emitter names or expected file names.
        var owners = ScopeSelection.Select(request).SelectMany(located => located.Slice.Commands.Select(command => command.Id)
            .Concat(located.Slice.Queries.Select(query => query.Id))
            .Concat(QueryPerformer.Queries(request, located).Select(query => query.Id))
            .Concat(located.Slice.Reactions.Select(reaction => reaction.Id))).ToHashSet();
        var before = given.executable_entries.From(baseline).Where(entry => entry.Sources.Any(owners.Contains)).ToArray();
        Assert.NotEmpty(before);
        foreach (var id in owners) Assert.Contains(before, entry => entry.Sources.Contains(id));
        var after = given.executable_entries.From(plan).Where(entry => entry.Sources.Any(owners.Contains)).ToArray();
        foreach (var entry in after)
        {
            Assert.True(entry.Declaration.Contains("@Secured({})", StringComparison.Ordinal) || entry.Declaration.Contains("@PreAuthorize(\"denyAll()\")", StringComparison.Ordinal), entry.Path + "\n" + entry.Declaration);
        }
        foreach (var reaction in ScopeSelection.Select(request).SelectMany(located => located.Slice.Reactions))
        {
            Assert.DoesNotContain(after, entry => entry.Sources.Contains(reaction.Id));
            Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == "STAGE-AXON-AUTHORIZATION"
                && diagnostic.Message.Contains("Availability gates are not rendered; reaction is not run", StringComparison.Ordinal));
        }
    }
}
