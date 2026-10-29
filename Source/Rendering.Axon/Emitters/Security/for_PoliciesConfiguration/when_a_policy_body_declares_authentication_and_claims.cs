// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_PoliciesConfiguration;

public class when_a_policy_body_declares_authentication_and_claims : Specification
{
    AxonEmissionContext _context;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/axon-samples/policy-locals");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["policy-locals.play"], null, "PolicyLocals");
        Assert.True(compilation.Success, string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        var loaded = compilation.Loaded!;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        _context = new(request, new(request.Profile));
    }

    void Because() => new AxonSecurityEmitter().Emit(_context);

    [Fact] void should_rename_the_authentication_parameter() => Policy().ShouldContain("check(Authentication authenticationInput)");
    [Fact] void should_not_introduce_an_implicit_claims_binding() => Policy().ShouldNotContain("claimsInput");
    [Fact] void should_keep_the_body_with_imported_type_spelling() => Policy().ShouldContain("var authentication = SecurityContextHolder");
    [Fact] void should_keep_the_pattern_variable_as_written() => Policy().ShouldContain("principal instanceof Map<?, ?> claims ? claims.get(\"department\")");
    [Fact] void should_emit_a_spring_policy_bean() => Policy().ShouldContain("@Component(\"isFinanceStaffPolicy\")");

    string Policy() => Source("IsFinanceStaffPolicy");
    string Source(string name) => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
