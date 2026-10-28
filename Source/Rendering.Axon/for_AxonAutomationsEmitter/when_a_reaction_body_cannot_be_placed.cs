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
    void should_report_the_reaction_as_not_registered_without_repeating_the_reason()
    {
        var context = Render(false);
        var diagnostic = context.Degradation.UnsupportedConstructs.Single(candidate => candidate.Code == "STAGE-AXON-0504");
        diagnostic.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("Notifier");
        diagnostic.Message.ShouldNotContain("language 'csharp'");
        context.Degradation.Refusals.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    void should_report_the_language_reason_once_through_the_audit()
    {
        var context = Render(false);
        var reported = context.Degradation.UnsupportedConstructs.Where(candidate => candidate.Message.Contains("language 'csharp' is not supported", StringComparison.Ordinal)).ToArray();
        reported.Length.ShouldEqual(1);
        reported[0].Code.ShouldEqual(AxonRefusalCodes.ImplementationLanguage);
    }

    [Fact]
    void should_keep_diagnostics_and_intents_aligned()
    {
        var context = Render(false);
        context.Degradation.UnsupportedConstructs.Select(diagnostic => diagnostic.Code).ShouldEqual(context.Degradation.Intents.Select(intent => intent.Code));
    }

    [Fact]
    void should_carry_the_reason_once_in_the_generated_comments() =>
        Comments(Render(false)).Split("language 'csharp' is not supported").Length.ShouldEqual(2);

    [Fact]
    void should_carry_the_reason_once_in_the_unsupported_summary()
    {
        var context = Render(false);
        UnsupportedSummary.Emit(context);
        var rows = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan()).Split('\n').Where(line => line.Contains("language 'csharp' is not supported", StringComparison.Ordinal)).ToArray();
        rows.Length.ShouldEqual(1);
        rows[0].StartsWith($"| {AxonRefusalCodes.ImplementationLanguage}", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    void should_refuse_in_strict_mode()
    {
        var codes = Render(true).Degradation.Refusals.SelectMany(refusal => refusal.Codes).ToArray();
        codes.ShouldContain("STAGE-AXON-0504");
        codes.ShouldContain(AxonRefusalCodes.ImplementationLanguage);
    }

    AxonEmissionContext Render(bool strict)
    {
        var request = _request with { Profile = AxonRendering.CreateProfile(strict: strict) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonAutomationsEmitter().Emit(context);
        InlineImplementationDiagnostics.Emit(context);
        return context;
    }

    static string Comments(AxonEmissionContext context) =>
        string.Concat(context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)).Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())));
}
