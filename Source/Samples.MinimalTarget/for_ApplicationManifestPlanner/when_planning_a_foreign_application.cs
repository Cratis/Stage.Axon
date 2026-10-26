// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Samples.MinimalTarget.for_ApplicationManifestPlanner;

public class when_planning_a_foreign_application : given.an_application_request
{
    Exception exception;
    ArtifactRenderPlan? plan;

    void Establish()
    {
        var foreign = SemanticId.Create(SemanticAddress.ForApplication(ApplicationIdentity.Create("Foreign")));
        request = request with { Scope = new(ArtifactRenderScopeKind.Application, foreign) };
    }

    void Because() => exception = Record.Exception(() => plan = planner.Plan(request));

    [Fact] void should_reject_the_foreign_identity() => Assert.IsType<InvalidArtifactRenderContract>(exception);
    [Fact] void should_not_return_an_output_plan() => plan.ShouldBeNull();
}
