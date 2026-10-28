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
        diagnostic.Message.ShouldContain("semantic owner does not match");
        context.Degradation.Refusals.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    void should_carry_the_specific_reason_in_the_generated_comment() =>
        Java(Render(false)).ShouldContain("semantic owner does not match");

    [Fact]
    void should_carry_the_specific_reason_into_the_unsupported_summary()
    {
        var context = Render(false);
        UnsupportedSummary.Emit(context);
        var row = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan()).Split('\n').Single(line => line.StartsWith("| STAGE-AXON-0504", StringComparison.Ordinal));
        row.ShouldContain("Notifier");
        row.ShouldContain("semantic owner does not match");
    }

    [Fact]
    void should_refuse_in_strict_mode() =>
        Render(true).Degradation.Refusals.Any(refusal => refusal.Codes.Contains("STAGE-AXON-0504")).ShouldBeTrue();

    [Fact]
    void should_name_a_foreign_language_once_the_body_is_owned_by_the_reaction()
    {
        var located = ScopeSelection.Select(_request).Single(candidate => !candidate.Slice.Reactions.IsEmpty);
        var reaction = located.Slice.Reactions[0];
        var owner = SemanticAddress.ForReaction(SemanticAddress.ForSlice(ApplicationIdentity.Create(_request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name), reaction.Name);
        var requirement = _request.ImplementationRequirements.Single() with { Owner = owner, Source = new(reaction.Id, default, default) };
        _request = _request with { ImplementationRequirements = [requirement] };
        var context = Render(false);
        context.Degradation.UnsupportedConstructs.Single(candidate => candidate.Code == "STAGE-AXON-0504").Message.ShouldContain("language 'csharp' is not supported");
        context.Degradation.UnsupportedConstructs.Count(candidate => candidate.Code == AxonRefusalCodes.ImplementationLanguage).ShouldEqual(1);
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
