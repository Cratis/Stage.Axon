// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Conformance.for_RenderCoverage;

public class when_checking_selected_scopes : given.a_render_request
{
    SemanticId first;
    SemanticId second;
    SemanticId[] reachable;
    CoverageReport report;

    void Establish()
    {
        var slices = SemanticIdentities.Definitions(request.Model).Values.OfType<SemanticSlice>().Where(slice => !slice.Commands.IsEmpty).ToArray();
        first = slices[0].Id;
        second = slices[1].Id;
        request = request with { Scope = new(ArtifactRenderScopeKind.Slice, first), AdditionalScopes = [new(ArtifactRenderScopeKind.Slice, second)] };
    }

    void Because()
    {
        reachable = [.. SemanticIdentities.Reachable(request)];
        report = RenderCoverage.Check(request, Plan([PlannedArtifact.CreateText("selected.txt", "realized", [.. reachable])], []), new HashSet<string>());
    }

    [Fact] void should_include_the_primary_scope() => reachable.ShouldContain(first);
    [Fact] void should_include_additional_scopes() => reachable.ShouldContain(second);
    [Fact] void should_not_require_an_unselected_application_root() => reachable.ShouldNotContain(request.Model.Application.Id);
    [Fact] void should_account_for_the_selected_reachable_graph() => report.IsClean.ShouldBeTrue();
}
