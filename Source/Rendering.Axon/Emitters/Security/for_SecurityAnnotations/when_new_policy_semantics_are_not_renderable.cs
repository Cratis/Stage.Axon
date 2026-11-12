// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_new_policy_semantics_are_not_renderable : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData("state")]
    [InlineData("and")]
    [InlineData("or")]
    [InlineData("not")]
    [InlineData("reads")]
    [InlineData("inputs")]
    [InlineData("mappings")]
    [InlineData("mapped-or")]
    [InlineData("mapped-and")]
    void should_deny_at_both_endpoint_and_handler(string construct)
    {
        var state = new SemanticStatePolicyCondition(new SemanticComparison(new(default, new SemanticTextValue("a")), SemanticComparisonOperator.Equal, new(default, new SemanticTextValue("a"))));
        SemanticPolicyCondition condition = construct switch
        {
            "state" => state,
            "and" => new SemanticLogicalPolicyCondition(new SemanticRoleCondition("Sales"), SemanticLogicalOperator.And, state),
            "or" => new SemanticLogicalPolicyCondition(new SemanticRoleCondition("Sales"), SemanticLogicalOperator.Or, state),
            "not" => new SemanticNotPolicyCondition(state),
            _ => new SemanticRoleCondition("Sales")
        };
        var policy = new SemanticPolicy("NewPolicy", condition);
        if (construct == "reads") policy = policy with { Reads = [new("state", ScopeSelection.Select(request).SelectMany(located => located.Slice.ReadModels).First().Id, "id")] };
        if (construct == "inputs") policy = policy with { Inputs = [new("id", new(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Text, default, false, false))] };
        var application = request.Model.Application with { Policies = [.. request.Model.Application.Policies, policy] };
        var changed = request with { Model = ExecutableSemanticModel.Create(LanguageVersion.V50, SemanticVersion.V50, application) };
        var context = new AxonEmissionContext(changed, new(changed.Profile));
        SemanticAuthorization authorization = new SemanticPolicyReference("NewPolicy");
        if (construct.StartsWith("mapped", StringComparison.Ordinal) || construct == "mappings") authorization = new SemanticPolicyReference("NewPolicy") { Mappings = [new("id", "orderId")] };
        if (construct == "mapped-or") authorization = new SemanticLogicalAuthorization(new SemanticPolicyReference("IsSales"), SemanticLogicalOperator.Or, authorization);
        if (construct == "mapped-and") authorization = new SemanticLogicalAuthorization(new SemanticPolicyReference("IsSales"), SemanticLogicalOperator.And, authorization);

        SecurityAnnotations.For(context, authorization, application.Id).ShouldContain("@PreAuthorize(\"denyAll()\")");
        SecurityAnnotations.ForHandler(context, authorization, application.Id, new JavaCodeBuilder("example")).ShouldContain("@Secured({})");
        context.Degradation.UnsupportedConstructs.ShouldNotBeEmpty();
        context.Degradation.UnsupportedConstructs.All(diagnostic => diagnostic.Code == "STAGE-AXON-AUTHORIZATION").ShouldBeTrue();
    }
}
