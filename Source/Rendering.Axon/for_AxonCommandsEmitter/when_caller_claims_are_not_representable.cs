// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_caller_claims_are_not_representable : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    const string Code = "STAGE-AXON-SPEC-CLAIMS";

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
        var caller = new SemanticCaller(form != "anonymous", [], [])
        {
            Claims = form switch
            {
                "other" => [new("tenant", "north")],
                "additional" => [new("sub", "fixture-user"), new("tenant", "north")],
                "duplicate" => [new("sub", "fixture-user"), new("sub", "another-user")],
                _ => [new("sub", "fixture-user")]
            }
        };
        var (context, java, specification) = Render(caller, strict);
        var diagnostic = Assert.Single(context.Degradation.UnsupportedConstructs, diagnostic => diagnostic.Code == Code);
        Assert.Equal(specification.Id, diagnostic.Artifact);
        Assert.Equal(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains($"// Screenplay wanted to preserve all modeled caller claims in the security fixture ({specification.Id}/givenCaller/claims), but this is not supported by the Axon target yet ({Code}).", java, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new UnsupportedOperationException", java, StringComparison.Ordinal);
        UnsupportedSummary.Emit(context);
        var summary = Text(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md"));
        Assert.Matches($@"\| {Code} \| [^\n]*\(givenCaller/claims\) \| [^\n]*\| preserve all modeled caller claims", summary);
        Assert.Equal(strict, context.Degradation.Refusals.Any(refusal => refusal.Codes.Contains(Code)));
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")]
    public void should_preserve_a_single_supported_subject_without_degradation(string type)
    {
        var (context, java, _) = Render(new(true, [], []) { Claims = [new(type, "fixture-user")] }, false);
        Assert.DoesNotContain(context.Degradation.UnsupportedConstructs, diagnostic => diagnostic.Code == Code);
        Assert.Contains("UsernamePasswordAuthenticationToken.authenticated(\"fixture-user\", null, List.of(", java, StringComparison.Ordinal);
    }

    (AxonEmissionContext Context, string Java, SemanticSpecification Specification) Render(SemanticCaller caller, bool strict)
    {
        var profile = AxonRendering.CreateProfile(strict: strict);
        var context = new AxonEmissionContext(request with { Profile = profile }, new(profile));
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var command = located.Slice.Commands[0];
        var specification = located.Slice.Specifications[0] with { GivenCaller = caller };
        var slice = located with { Slice = located.Slice with { Specifications = [specification] } };
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        var usesClock = new CommandHandlerEmitter(context, slice, command, source).Emit();
        new CommandSpecificationsEmitter(context, slice, command, source, usesClock).Emit();
        return (context, Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SpecificationsTest.java", StringComparison.Ordinal))), specification);
    }

    static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
}
