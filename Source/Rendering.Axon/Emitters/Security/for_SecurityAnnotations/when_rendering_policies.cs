// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_rendering_policies : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Establish() => context = new(request, new AxonDegradation(request.Profile));

    [Fact]
    void should_preserve_the_absence_of_authorization() =>
        SecurityAnnotations.For(context, null, loaded.Model.Application.Id).ShouldEqual(string.Empty);

    [Fact]
    void should_render_a_named_role_policy() =>
        SecurityAnnotations.For(context, new SemanticPolicyReference("IsSales"), loaded.Model.Application.Id).ShouldEqual("@PreAuthorize(\"hasRole('Sales')\")");

    [Fact]
    void should_fail_closed_when_a_policy_is_unknown()
    {
        var annotation = SecurityAnnotations.For(context, new SemanticPolicyReference("Missing"), loaded.Model.Application.Id);
        annotation.ShouldContain("@PreAuthorize(\"denyAll()\")");
        annotation.ShouldContain("// Screenplay wanted to authorize access");
        context.Degradation.UnsupportedConstructs.Count.ShouldEqual(1);
        context.Degradation.Intents.Single().ModelPath.ShouldContain("/authorization");
    }

    [Fact]
    void should_fail_closed_for_an_or_expression_containing_an_unknown_policy()
    {
        var authorization = new SemanticLogicalAuthorization(new SemanticPolicyReference("IsSales"), SemanticLogicalOperator.Or, new SemanticPolicyReference("Missing"));
        SecurityAnnotations.For(context, authorization, loaded.Model.Application.Id).ShouldContain("@PreAuthorize(\"denyAll()\")");
    }
}
