// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_rendering_an_inline_java_policy : Specification
{
    AxonEmissionContext context;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["inline-java-policies.play"], null, "InlinePolicies");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded!;
        var profile = AxonRendering.WithBearerTokens(AxonRendering.CreateProfile());
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, profile, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        context = new(request, new(request.Profile));
    }

    [Fact]
    void should_call_the_native_bean_without_degradation()
    {
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldEqual("@PreAuthorize(\"isAuthenticated() and @isFinanceStaffPolicy.check(authentication)\")");
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SemanticLogicalOperator.And)]
    [InlineData(SemanticLogicalOperator.Or)]
    void should_deny_whole_compositions_instead_of_broadening_on_failure(SemanticLogicalOperator operation)
    {
        Annotation(new SemanticLogicalAuthorization(new SemanticPolicyReference("IsWriter"), operation, new SemanticPolicyReference("FinanceCheck"))).ShouldContain("denyAll()");
        context.Degradation.UnsupportedConstructs.Single().Message.ShouldContain("composition");
    }

    [Fact]
    void should_emit_only_native_jwt_claims()
    {
        new AxonSecurityEmitter().Emit(context);
        var code = Source("IsFinanceStaffPolicy");
        code.ShouldContain("public boolean check(Authentication authentication)");
        code.ShouldContain("jwt.getTokenAttributes()");
        code.ShouldNotContain("getDetails()");
        code.ShouldNotContain("putIfAbsent");
        code.ShouldNotContain("catch (Throwable");
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/Policies.java", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("return !claims.containsKey(\"blocked\");")]
    [InlineData("return authentication.getDetails() != null;")]
    [InlineData("return claims.get(\"sub\") == null;")]
    [InlineData("return java.util.Collections.emptyList().isEmpty();")]
    void should_catalog_unsafe_binding_or_unimportable_bodies_as_denial(string body)
    {
        Body(body);
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldContain("denyAll()");
        context.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual("STAGE-AXON-AUTHORIZATION");
        new AxonSecurityEmitter().Emit(context);
        context.ConsumedImplementations.Contains(context.Request.ImplementationRequirements.Single(item => item.Member == "IsFinanceStaff").RequirementId).ShouldBeFalse();
    }

    [Fact]
    void should_deny_implicit_claims_without_a_native_provider()
    {
        var request = context.Request with { Profile = AxonRendering.CreateProfile() };
        context = new(request, new(request.Profile));
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldContain("denyAll()");
    }

    [Fact]
    void should_not_emit_an_automatic_demo_identity()
    {
        new AxonSecurityEmitter().Emit(context);
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/DemoIdentityConfiguration.java", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    void should_isolate_scope_escaping_bodies_from_siblings()
    {
        new AxonSecurityEmitter().Emit(context);
        Source("ScopeEscapePolicy").ShouldContain("private static final ForgedDecision Decision = new ForgedDecision();");
        Source("IsFinanceStaffPolicy").ShouldNotContain("ForgedDecision");
    }

    [Theory]
    [InlineData(false, "// \\u007d\nreturn true;")]
    [InlineData(true, "// \\u007d\nreturn true;")]
    [InlineData(false, "return \"\\u0061\".isEmpty();")]
    [InlineData(true, "return \"\\u0061\".isEmpty();")]
    void should_reject_unicode_escapes_without_emitting_or_consuming_the_body(bool strict, string body)
    {
        Body(body, AxonRendering.CreateProfile(strict: strict));
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldContain("STAGE-AXON-AUTHORIZATION");
        context.Degradation.UnsupportedConstructs.Single().Message.ShouldContain("Java Unicode escapes");
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
        new AxonSecurityEmitter().Emit(context);
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/IsFinanceStaffPolicy.java", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_preserve_foreign_language_diagnostics(bool strict)
    {
        var request = context.Request with { Profile = AxonRendering.CreateProfile(strict: strict), ImplementationRequirements = [context.Request.ImplementationRequirements.Single(item => item.Member == "IsFinanceStaff") with { Language = "python" }] };
        context = new(request, new(request.Profile));
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldContain("denyAll()");
        InlineImplementationDiagnostics.Emit(context);
        string.Join(' ', context.Degradation.UnsupportedConstructs.Select(item => item.Message)).ShouldContain("language 'python'");
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
    }

    void Body(string body, ArtifactRenderProfile? profile = null)
    {
        var requirement = context.Request.ImplementationRequirements.Single(item => item.Member == "IsFinanceStaff");
        var request = context.Request with
        {
            Profile = profile ?? context.Request.Profile,
            ImplementationContents = context.Request.ImplementationContents.SetItem(requirement.RequirementId, body),
            ImplementationRequirements = [.. context.Request.ImplementationRequirements.Select(item => item.RequirementId == requirement.RequirementId
                ? item with { ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))) } : item)]
        };
        context = new(request, new(request.Profile));
    }

    string Annotation(SemanticAuthorization authorization) => SecurityAnnotations.For(context, authorization, context.Request.Model.Application.Id);
    string Source(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
