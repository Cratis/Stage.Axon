// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_a_reaction_body_cannot_be_placed : Specification
{
    const string Model = """
        module Notifications
          feature Counters
            slice StateChange Increase
              command Increase
                amount Int
                produces Increased
                  amount = amount
              event Increased
                amount Int

            slice Automation Notify
              reaction Notifier
                when Increased
                  ```csharp
                  return;
                  ```
        """;

    string _directory;
    ArtifactRenderRequest _request;

    async Task Establish()
    {
        _directory = Path.Combine(Path.GetTempPath(), "axon-reaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "notifications.play"), Model.ReplaceLineEndings("\n"));
        var loaded = await SemanticModelLoader.LoadFromPathAsync(_directory, null, "AxonReactions");
        _request = new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
    }

    void Destroy() => Directory.Delete(_directory, true);

    [Fact]
    void should_parse_a_reaction_body_requirement() =>
        _request.ImplementationRequirements.Count(requirement => requirement.Role == SemanticImplementationRole.ReactionEffect).ShouldEqual(1);

    [Fact]
    void should_carry_the_specific_reason_in_the_not_registered_warning()
    {
        var context = Render(false);
        var diagnostic = context.Degradation.UnsupportedConstructs.Single(candidate => candidate.Code == "STAGE-AXON-0504");
        diagnostic.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("Notifier");
        diagnostic.Message.ShouldContain("language 'csharp' is not supported");
        context.Degradation.Refusals.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    void should_carry_the_specific_reason_in_the_generated_comment() =>
        Java(Render(false)).ShouldContain("language 'csharp' is not supported");

    [Fact]
    void should_carry_the_specific_reason_into_the_unsupported_summary()
    {
        var context = Render(false);
        UnsupportedSummary.Emit(context);
        var row = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan()).Split('\n').Single(line => line.StartsWith("| STAGE-AXON-0504", StringComparison.Ordinal));
        row.ShouldContain("Notifier");
        row.ShouldContain("language 'csharp' is not supported");
    }

    [Fact]
    void should_refuse_in_strict_mode() =>
        Render(true).Degradation.Refusals.Any(refusal => refusal.Codes.Contains("STAGE-AXON-0504")).ShouldBeTrue();

    [Fact]
    void should_report_the_reason_once_because_the_slice_owned_body_is_not_in_the_unplaced_audit()
    {
        var context = Render(false);
        context.Degradation.UnsupportedConstructs.Count(candidate => candidate.Message.Contains("language 'csharp' is not supported", StringComparison.Ordinal)).ShouldEqual(1);
        context.Degradation.UnsupportedConstructs.Any(candidate => candidate.Code == AxonRefusalCodes.ImplementationLanguage || candidate.Code == AxonRefusalCodes.Implementation).ShouldBeFalse();
    }

    AxonEmissionContext Render(bool strict)
    {
        var request = _request with { Profile = AxonRendering.CreateProfile(strict: strict) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonAutomationsEmitter().Emit(context);
        InlineImplementationDiagnostics.Emit(context);
        return context;
    }

    static string Java(AxonEmissionContext context) =>
        string.Concat(context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith("/NotifierReaction.java", StringComparison.Ordinal)).Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())));
}
