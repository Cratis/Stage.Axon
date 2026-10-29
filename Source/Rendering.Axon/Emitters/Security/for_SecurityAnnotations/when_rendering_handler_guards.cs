// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_rendering_handler_guards : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    JavaCodeBuilder code;

    void Establish()
    {
        context = new(request, new(request.Profile));
        code = new("example");
    }

    [Fact] void should_preserve_public_handlers() => Guard(null).ShouldEqual(string.Empty);
    [Fact] void should_use_native_metadata_roles() => Guard(new SemanticPolicyReference("IsSales")).ShouldEqual("@Secured({\"ROLE_Sales\"})");
    [Fact]
    void should_authorize_subscriptions_at_opening_without_degradation()
    {
        var live = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Queries).First(query => query.Delivery == SemanticQueryDelivery.Live);
        SecurityAnnotations.ForHandler(context, new SemanticPolicyReference("IsSales"), live.Id, code).ShouldEqual("@Secured({\"ROLE_Sales\"})");
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Fact]
    void should_import_the_native_annotation()
    {
        Guard(new SemanticPolicyReference("IsSales"));
        code.Build().ShouldContain("import org.springframework.security.access.annotation.Secured;");
    }

    [Fact]
    void should_catalog_unknown_policies_and_deny_even_with_metadata()
    {
        Guard(new SemanticPolicyReference("Missing")).ShouldContain("@Secured({})");
        context.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual("STAGE-AXON-AUTHORIZATION");
        context.Degradation.Intents.Single().ModelPath.ShouldContain("/authorization");
    }

    [Fact] void should_enforce_authenticated_at_the_handler() => AssertEndpointGuard(new SemanticAuthenticatedCondition(), "isAuthenticated()");
    [Fact] void should_enforce_not_at_the_handler() => AssertEndpointGuard(new SemanticNotPolicyCondition(new SemanticRoleCondition("Sales")), "!(hasRole('Sales'))");
    [Fact] void should_enforce_authenticated_not_at_the_handler() => AssertEndpointGuard(new SemanticLogicalPolicyCondition(new SemanticAuthenticatedCondition(), SemanticLogicalOperator.And, new SemanticNotPolicyCondition(new SemanticRoleCondition("Sales"))), "(isAuthenticated()) and (!(hasRole('Sales')))");
    [Fact] void should_enforce_distinct_role_and_at_the_handler() => AssertEndpointGuard(new SemanticLogicalPolicyCondition(new SemanticRoleCondition("Sales"), SemanticLogicalOperator.And, new SemanticRoleCondition("Writer")), "(hasRole('Sales')) and (hasRole('Writer'))");

    [Fact]
    void should_not_use_handler_thread_authentication() => Guard(new SemanticPolicyReference("Missing")).ShouldNotContain("PreAuthorize");

    void AssertEndpointGuard(SemanticPolicyCondition condition, string expression)
    {
        var application = request.Model.Application with { CommandReadMode = SemanticCommandReadMode.Protected, Policies = [.. request.Model.Application.Policies, new("Unsupported", condition)] };
        var model = ExecutableSemanticModel.Create(new(25, 0), SemanticVersion.V25, application);
        var fixture = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        var annotation = SecurityAnnotations.ForHandler(fixture, new SemanticPolicyReference("Unsupported"), application.Id, code);
        annotation.ShouldNotContain("@Secured");
        code.Build().ShouldNotContain("import org.springframework.security.access.annotation.Secured;");
        annotation.ShouldEqual("@PreAuthorize(\"" + expression + "\")");
        code.Build().ShouldContain("import org.springframework.security.access.prepost.PreAuthorize;");
        SecurityAnnotations.For(fixture, new SemanticPolicyReference("Unsupported"), application.Id).ShouldEqual("@PreAuthorize(\"" + expression + "\")");
        fixture.Degradation.UnsupportedConstructs.ShouldBeEmpty();
        fixture.Degradation.Warnings.ShouldBeEmpty();
        UnsupportedSummary.Emit(fixture);
        Encoding.UTF8.GetString(fixture.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan()).ShouldContain("No unsupported constructs.");
    }

    string Guard(SemanticAuthorization? authorization) => SecurityAnnotations.ForHandler(context, authorization, loaded.Model.Application.Id, code);
}
