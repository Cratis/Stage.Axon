// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_PoliciesConfiguration;

public class when_a_policy_guards_a_command : Specification
{
    AxonEmissionContext _context;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/axon-samples/policy-locals");
        var compilation = await SemanticModelLoader.LoadAsync(root, ["policy-locals.play"], null, "PolicyLocals");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded!;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        _context = new(request, new(request.Profile));
    }

    void Because() => new AxonSecurityEmitter().Emit(_context);

    [Fact] void should_emit_a_named_spring_bean() => Policy().ShouldContain("@Component(\"isFinanceStaffPolicy\")");
    [Fact] void should_expose_a_native_authentication_method() => Policy().ShouldContain("public boolean check(Authentication authenticationInput) throws Exception");
    [Fact] void should_lower_known_types_into_imports() => Policy().ShouldContain("var authentication = SecurityContextHolder.getContext().getAuthentication();");
    [Fact] void should_not_emit_a_dispatcher() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/Policies.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact]
    void should_authorize_through_the_bean() => SecurityAnnotations.For(_context, new Cratis.Screenplay.Semantics.SemanticPolicyReference("IsFinanceStaff"), _context.Request.Model.Application.Id)
        .ShouldEqual("@PreAuthorize(\"isAuthenticated() and @isFinanceStaffPolicy.check(authentication)\")");
    [Fact] void should_account_for_the_emitted_body() => _context.ConsumedImplementations.Count.ShouldEqual(1);
    [Fact] void should_fail_closed_if_the_authored_policy_throws() => Policy().ShouldContain("// Fail closed: a policy that throws denies access.\n        catch (Exception | Error failure)");

    [Fact]
    void should_leave_the_billing_fqn_policy_command_handler_unguarded_without_unsupported_diagnostics()
    {
        // Same SecurityContextHolder and Map-pattern policy body as L62's model-billing-java.
        var command = ScopeSelection.Select(_context.Request).SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "RegisterAccount");
        SecurityAnnotations.For(_context, command.Authorization, command.Id).ShouldEqual("@PreAuthorize(\"isAuthenticated() and @isFinanceStaffPolicy.check(authentication)\")");
        SecurityAnnotations.ForHandler(_context, command.Authorization, command.Id, new JavaCodeBuilder("example")).ShouldNotContain("@Secured");
        _context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
        _context.Degradation.Warnings.Single().Code.ShouldEqual("STAGE-AXON-AUTHORIZATION");
        Policy().ShouldContain("import org.springframework.security.core.context.SecurityContextHolder;");
        Policy().ShouldContain("import java.util.Map;");
    }

    string Policy() => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/IsFinanceStaffPolicy.java", StringComparison.Ordinal)).Bytes.AsSpan());
}
