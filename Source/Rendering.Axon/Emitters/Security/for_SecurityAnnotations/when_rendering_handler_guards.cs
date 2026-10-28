// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    [Fact]
    void should_not_use_handler_thread_authentication() => Guard(new SemanticPolicyReference("Missing")).ShouldNotContain("PreAuthorize");

    string Guard(SemanticAuthorization? authorization) => SecurityAnnotations.ForHandler(context, authorization, loaded.Model.Application.Id, code);
}
