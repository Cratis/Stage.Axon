// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        context = new(request, new(request.Profile));
    }

    [Fact]
    void should_call_the_bean_without_degradation()
    {
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldEqual(SiteAnnotation);
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Fact]
    void should_combine_with_roles() => Annotation(new SemanticLogicalAuthorization(new SemanticPolicyReference("IsWriter"), SemanticLogicalOperator.And, new SemanticPolicyReference("IsFinanceStaff")))
        .ShouldEqual(SiteAnnotation);

    [Fact]
    void should_pin_the_upstream_refusal_of_negated_inline_policies()
    {
        var policy = context.Request.Model.Application.Policies.Single(policy => policy.Name == "IsFinanceStaff");
        var application = context.Request.Model.Application with { Policies = [.. context.Request.Model.Application.Policies.Where(candidate => candidate != policy), policy with { Condition = new SemanticNotPolicyCondition(policy.Condition) }] };
        var exception = Assert.Throws<InvalidSemanticContract>(() => ExecutableSemanticModel.Create(new(7, 0), new(7, 0), application));
        exception.Message.ShouldEqual("Invalid policy condition.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_emit_the_claims_adapter_only_with_selected_dependencies(bool bearer)
    {
        var request = context.Request with { Profile = bearer ? AxonRendering.WithBearerTokens(context.Request.Profile) : context.Request.Profile };
        context = new(request, new(request.Profile));
        new AxonSecurityEmitter().Emit(context);
        var code = Source("Policies");
        code.ShouldContain("@Component(\"policies\")");
        code.ShouldContain("public boolean authorize");
        code.ShouldContain("private static boolean isFinanceStaff(Authentication authentication, Map<String, Object> claims)");
        code.ShouldContain("catch (RuntimeException");
        code.ShouldContain("return false;");
        code.ShouldContain("claims.putIfAbsent");
        code.ShouldContain("authentication instanceof AnonymousAuthenticationToken");
        code.ShouldContain("authentication.getDetails() instanceof Map<?, ?>");
        code.ShouldContain("return claims.get(\"department\") instanceof String department && department.startsWith(\"Finance\");");
        code.Contains("jwt.getTokenAttributes()", StringComparison.Ordinal).ShouldEqual(bearer);
        context.ConsumedImplementations.Count.ShouldEqual(2);
        InlineImplementationDiagnostics.Emit(context);
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Fact]
    void should_not_emit_unreferenced_bodies()
    {
        var request = context.Request with { ImplementationRequirements = [context.Request.ImplementationRequirements.Single(item => item.Member == "IsFinanceStaff") with { RequirementId = "unreferenced" }] };
        context = new(request, new(request.Profile));
        new AxonSecurityEmitter().Emit(context);
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/Policies.java", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    void should_deny_the_second_colliding_member()
    {
        var policies = context.Request.Model.Application.Policies;
        var policy = policies.Single(policy => policy.Name == "IsFinanceStaff");
        ReplacePolicies([.. policies, policy with { Name = "isFinanceStaff", Condition = new SemanticOpaquePolicyCondition("collision") }]);
        Annotation(new SemanticPolicyReference("isFinanceStaff")).ShouldContain("denyAll()");
        context.Degradation.UnsupportedConstructs.Single().Message.ShouldContain("collision");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_preserve_the_foreign_language_deny_and_diagnostics(bool strict)
    {
        var request = context.Request with { Profile = AxonRendering.CreateProfile(strict: strict), ImplementationRequirements = [context.Request.ImplementationRequirements.Single(item => item.Member == "IsFinanceStaff") with { Language = "python" }] };
        context = new(request, new(request.Profile));
        Annotation(new SemanticPolicyReference("IsFinanceStaff")).ShouldContain("denyAll()");
        InlineImplementationDiagnostics.Emit(context);
        context.Degradation.UnsupportedConstructs.Count.ShouldEqual(2);
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
        string.Join(' ', context.Degradation.UnsupportedConstructs.Select(item => item.Message)).ShouldContain("language 'python'");
    }

    [Fact]
    void should_evaluate_every_branch_and_propagate_failure_through_negation()
    {
        new AxonSecurityEmitter().Emit(context);
        var code = Source("Policies");
        code.ShouldContain("private enum Decision { ALLOW, DENY, FAILED }");
        code.ShouldContain("return value == Decision.FAILED ? Decision.FAILED : decision(value == Decision.DENY);");
        code.ShouldContain("if (left == Decision.FAILED || right == Decision.FAILED) return Decision.FAILED;");
        code.ShouldContain("catch (Exception failure)");
        code.ShouldContain("return Decision.FAILED;");
        code.ShouldContain("if (!authenticated(authentication)) return false;");
        code.ShouldContain("return and(decision(authentication != null && authentication.getAuthorities().stream().anyMatch(authority -> authority.getAuthority().equals(\"ROLE_Writer\"))), evaluateFinanceCheck(authentication, claims)) == Decision.ALLOW;");
        code.ShouldContain("return or(decision(authentication != null && authentication.getAuthorities().stream().anyMatch(authority -> authority.getAuthority().equals(\"ROLE_Writer\"))), evaluateFinanceCheck(authentication, claims)) == Decision.ALLOW;");
        code.ShouldNotContain("return evaluateIsFinanceStaff(authentication, claims) &&");
    }

    [Fact]
    void should_keep_demo_claims_inside_the_inactive_profile()
    {
        new AxonSecurityEmitter().Emit(context);
        var demo = Source("DemoIdentityConfiguration");
        demo.ShouldContain("@Profile(\"demo\")");
        demo.ShouldContain("Binder.get(environment).bind(\"stage.axon.demo.claims\", Bindable.mapOf(String.class, String.class))");
        demo.ShouldContain("putIfAbsent(\"sub\", \"demo\")");
        demo.ShouldContain("token.setDetails(");
        context.Artifacts.Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())).ShouldEachConformTo(source => !source.Contains("spring.profiles.active", StringComparison.Ordinal));
    }

    string SiteAnnotation => "@PreAuthorize(\"@policies." + PolicyPredicates.SiteMember(context.Request.Model.Application.Id) + "(authentication)\")";
    string Annotation(SemanticAuthorization authorization) => SecurityAnnotations.For(context, authorization, context.Request.Model.Application.Id);
    string Source(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());

    void ReplacePolicies(params SemanticPolicy[] policies)
    {
        var application = context.Request.Model.Application with { Policies = [.. policies] };
        var request = context.Request with { Model = ExecutableSemanticModel.Create(context.Request.Model.LanguageVersion, context.Request.Model.SemanticVersion, application) };
        context = new(request, new(request.Profile));
    }
}
