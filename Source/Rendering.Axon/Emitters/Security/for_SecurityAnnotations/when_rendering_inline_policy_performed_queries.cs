// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_rendering_inline_policy_performed_queries : Specification
{
    ArtifactRenderRequest _request;
    IReadOnlyDictionary<string, string> _sources;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source/Rendering.Axon.Verification/Corpus");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["inline-java-policy-performers.play"], null, "InlinePolicies");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded!;
        _request = new(loaded.Model, loaded.Plan, AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
    }

    void Because()
    {
        var plan = new AxonArtifactRenderPlanner().Plan(_request);
        plan.Refusals.ShouldBeEmpty();
        _sources = plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
            .ToDictionary(artifact => artifact.RelativePath, artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()), StringComparer.Ordinal);
    }

    [Fact]
    void should_generate_a_method_for_every_policy_reference_in_every_rendered_java_source()
    {
        Source("PerformedFinancePolicy").ShouldContain("public boolean check(Authentication authentication)");
        _sources.Keys.Any(path => path.EndsWith("/Policies.java", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    void should_use_the_performed_query_id_for_both_handler_and_endpoint_sites()
    {
        var requirements = _request.ImplementationRequirements.Where(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer).ToArray();
        requirements.Length.ShouldEqual(2);
        foreach (var requirement in requirements)
        {
            Source(requirement.Owner.Name + "Handler").ShouldContain("@PreAuthorize");
            Source(requirement.Owner.Name + "Endpoint").ShouldContain("@PreAuthorize");
        }
    }

    [Fact]
    void should_guard_non_http_performed_query_dispatch_at_the_handler()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        foreach (var query in ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.Queries))
        {
            SecurityAnnotations.ForHandler(context, query.Authorization, query.Id, new JavaCodeBuilder("example")).ShouldNotContain("@Secured");
            context.Degradation.Warnings.ShouldBeEmpty();
            Source(query.Name + "Handler").ShouldContain("@PreAuthorize(\"isAuthenticated() and @performedFinancePolicy.check(authentication)\")");
            Source(query.Name + "Handler").ShouldNotContain("@Secured");
            Source(query.Name + "Endpoint").ShouldContain("@PreAuthorize(\"isAuthenticated() and @performedFinancePolicy.check(authentication)\")");
        }
    }

    [Fact]
    void should_discover_policies_referenced_only_by_performed_queries() => Source("PerformedFinancePolicy")
        .ShouldContain("department.startsWith(\"Finance\")");

    [Fact]
    void should_deduplicate_performed_query_sites_by_query_id()
    {
        var requirement = _request.ImplementationRequirements.First(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer);
        var request = _request with { ImplementationRequirements = [.. _request.ImplementationRequirements, requirement] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        PolicyPredicates.Sites(context).Count(site => site.Owner == requirement.Source.SemanticId).ShouldEqual(1);
    }

    string Source(string name) => _sources.Single(source => source.Key.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Value;
}
