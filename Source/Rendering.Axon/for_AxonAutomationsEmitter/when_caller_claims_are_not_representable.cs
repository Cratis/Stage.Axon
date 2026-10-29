// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_caller_claims_are_not_representable : given.a_reaction_model
{
    const string DiagnosticCode = "STAGE-AXON-SPEC-CLAIMS";

    [Theory]
    [InlineData(false, "other")]
    [InlineData(true, "other")]
    [InlineData(false, "additional")]
    [InlineData(true, "additional")]
    [InlineData(false, "duplicate")]
    [InlineData(true, "duplicate")]
    [InlineData(false, "anonymous")]
    [InlineData(true, "anonymous")]
    public void should_report_the_dropped_claims_in_output_and_refuse_in_strict_mode(bool strict, string form)
    {
        Specify();
        specification = specification with
        {
            GivenCaller = new(form != "anonymous", [], [])
            {
                Roles = ["SYSTEM"],
                Claims = form switch
                {
                    "other" => [new("tenant", "north")],
                    "additional" => [new("sub", "fixture-user"), new("tenant", "north")],
                    "duplicate" => [new("sub", "fixture-user"), new("sub", "another-user")],
                    _ => [new("sub", "fixture-user")]
                }
            }
        };
        request = request with { Profile = AxonRendering.CreateProfile(strict: strict) };
        Render();
        var java = Code("SubmitOrderFiringSpecificationsTest");
        var diagnostic = Assert.Single(context.Degradation.UnsupportedConstructs, diagnostic => diagnostic.Code == DiagnosticCode);
        Assert.Equal(specification.Id, diagnostic.Artifact);
        Assert.Equal(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains($"// Screenplay wanted to preserve all modeled caller claims in the security fixture ({specification.Id}/givenCaller/claims), but this is not supported by the Axon target yet ({DiagnosticCode}).", java, StringComparison.Ordinal);
        if (form != "anonymous") Assert.DoesNotContain("throw new UnsupportedOperationException", java, StringComparison.Ordinal);
        UnsupportedSummary.Emit(context);
        var summary = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan());
        Assert.Matches($@"\| {DiagnosticCode} \| [^\n]*\(givenCaller/claims\) \| [^\n]*\| preserve all modeled caller claims", summary);
        Assert.Equal(strict, context.Degradation.Refusals.Any(refusal => refusal.Codes.Contains(DiagnosticCode)));
    }

    [Theory]
    [InlineData(true, "SYSTEM", true)]
    [InlineData(true, "Admin", false)]
    [InlineData(false, "SYSTEM", false)]
    public void should_only_run_callers_the_system_endpoint_admits(bool authenticated, string role, bool enabled)
    {
        Specify();
        specification = specification with { GivenCaller = new(authenticated, [], []) { Roles = [role] } };
        Render();
        var java = Code("SubmitOrderFiringSpecificationsTest");
        Assert.Equal(enabled, java.Contains("endpoint.execute(", StringComparison.Ordinal));
        Assert.Equal(!enabled, java.Contains("// Differs from Screenplay:", StringComparison.Ordinal));
        Assert.Equal(!enabled, java.Contains("@Disabled", StringComparison.Ordinal));
        Assert.Equal(!enabled, context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0504" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning));
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")]
    public void should_preserve_a_single_supported_subject_without_degradation(string type)
    {
        Specify();
        specification = specification with { GivenCaller = new(true, [], []) { Roles = ["SYSTEM"], Claims = [new(type, "fixture-user")] } };
        Render();
        Assert.DoesNotContain(context.Degradation.UnsupportedConstructs, diagnostic => diagnostic.Code == DiagnosticCode);
        Assert.Contains("UsernamePasswordAuthenticationToken.authenticated(\"fixture-user\", null, List.of(", Code("SubmitOrderFiringSpecificationsTest"), StringComparison.Ordinal);
    }
}
