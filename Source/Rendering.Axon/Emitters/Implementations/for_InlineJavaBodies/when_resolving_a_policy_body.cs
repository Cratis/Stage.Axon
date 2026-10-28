// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_resolving_a_policy_body : Specification
{
    AxonEmissionContext context;
    SemanticImplementationRequirement requirement;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["inline-java-policies.play"], null, "InlinePolicies");
        Assert.True(compilation.Success, string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        var loaded = compilation.Loaded!;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        context = new(request, new(request.Profile));
        requirement = loaded.ImplementationRequirements.Single();
    }

    [Fact] void should_have_the_policy_role() => requirement.Role.ShouldEqual(SemanticImplementationRole.PolicyPredicate);
    [Fact] void should_have_the_policy_member() => requirement.Member.ShouldEqual("IsFinanceStaff");
    [Fact] void should_be_application_owned() => requirement.Source.SemanticId.ShouldEqual(context.Request.Model.Application.Id);
    [Fact] void should_require_pure_execution() => requirement.RequiredCapability.ShouldEqual("pure");

    [Fact]
    void should_resolve_the_body()
    {
        InlineJavaBodies.TryResolve(context, new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out var body, out _).ShouldBeTrue();
        body.ShouldContain("return claims.get(\"department\") instanceof String department && department.startsWith(\"Finance\");");
    }

    [Fact]
    void should_reject_a_foreign_language_with_the_remedy()
    {
        var request = context.Request with { ImplementationRequirements = [requirement with { Language = "python" }] };
        InlineJavaBodies.TryResolve(new(request, new(request.Profile)), new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out _, out var reason).ShouldBeFalse();
        reason.ShouldContain("language 'python' is not supported. Supply the body in Java");
    }
}
