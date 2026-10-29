// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_rendering_absorbed_role_guards : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Fact] void should_absorb_the_module_alternatives() => Guard(And(Or(Role("Rep"), Role("Mgr")), Role("Mgr"))).ShouldEqual("@Secured({\"ROLE_Mgr\"})");
    [Fact] void should_absorb_the_command_alternatives() => Guard(And(Role("Mgr"), Or(Role("Rep"), Role("Mgr")))).ShouldEqual("@Secured({\"ROLE_Mgr\"})");
    [Fact] void should_keep_a_residual_conjunction_at_the_endpoint() => Guard(And(Or(Role("A"), Role("B")), Or(Role("A"), Role("C")))).ShouldContain("dispatch outside HTTP is not guarded");
    [Fact] void should_deduplicate_alternatives() => Guard(Or(Or(Role("A"), Role("B")), Role("A"))).ShouldEqual("@Secured({\"ROLE_A\", \"ROLE_B\"})");
    [Fact] void should_preserve_public_absorption() => GuardPublic(SemanticLogicalOperator.Or).ShouldEqual(string.Empty);
    [Fact] void should_preserve_public_conjunction() => GuardPublic(SemanticLogicalOperator.And).ShouldEqual("@Secured({\"ROLE_Sales\"})");

    [Fact]
    void should_reduce_logical_policy_references_for_queries()
    {
        var application = request.Model.Application with { CommandReadMode = SemanticCommandReadMode.Protected, Policies = [.. request.Model.Application.Policies, new("CanWork", Or(Role("Rep"), Role("Mgr"))), new("IsManager", Role("Mgr"))] };
        var model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        var query = application.Modules.SelectMany(module => module.Features).SelectMany(feature => feature.Slices).SelectMany(slice => slice.Queries).First();
        var authorization = new SemanticLogicalAuthorization(new SemanticPolicyReference("CanWork"), SemanticLogicalOperator.And, new SemanticPolicyReference("IsManager"));
        SecurityAnnotations.ForHandler(context, authorization, query.Id, new JavaCodeBuilder("example")).ShouldEqual("@Secured({\"ROLE_Mgr\"})");
        context.Degradation.Warnings.ShouldBeEmpty();
    }

    string Guard(SemanticPolicyCondition condition)
    {
        var application = request.Model.Application with { CommandReadMode = SemanticCommandReadMode.Protected, Policies = [.. request.Model.Application.Policies, new("Public", new SemanticAnonymousCondition()), new("Policy", condition)] };
        var model = ExecutableSemanticModel.Create(new(31, 0), SemanticVersion.V31, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        return SecurityAnnotations.ForHandler(context, new SemanticPolicyReference("Policy"), application.Id, new JavaCodeBuilder("example"));
    }

    string GuardPublic(SemanticLogicalOperator operation)
    {
        var application = request.Model.Application with { CommandReadMode = SemanticCommandReadMode.Protected, Policies = [.. request.Model.Application.Policies, new("Public", new SemanticAnonymousCondition())] };
        var model = ExecutableSemanticModel.Create(new(31, 0), SemanticVersion.V31, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        var authorization = new SemanticLogicalAuthorization(new SemanticPolicyReference("IsSales"), operation, new SemanticPolicyReference("Public"));
        return SecurityAnnotations.ForHandler(context, authorization, application.Id, new JavaCodeBuilder("example"));
    }

    static SemanticRoleCondition Role(string role) => new(role);
    static SemanticLogicalPolicyCondition And(SemanticPolicyCondition left, SemanticPolicyCondition right) => new(left, SemanticLogicalOperator.And, right);
    static SemanticLogicalPolicyCondition Or(SemanticPolicyCondition left, SemanticPolicyCondition right) => new(left, SemanticLogicalOperator.Or, right);
}
