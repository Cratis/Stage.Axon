// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Automations;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_auditing_a_placed_slice_owned_reaction_body : Specification
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
                  ```java
                  // placed body
                  ```
        """;

    string _directory;
    AxonEmissionContext _context;
    SemanticImplementationRequirement _requirement;

    async Task Establish()
    {
        _directory = Path.Combine(Path.GetTempPath(), "axon-placed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "notifications.play"), Model.ReplaceLineEndings("\n"));
        var loaded = await SemanticModelLoader.LoadFromPathAsync(_directory, null, "AxonPlaced");
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id))
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        };
        _requirement = request.ImplementationRequirements.Single(requirement => requirement.Role == SemanticImplementationRole.ReactionEffect);
        _context = new AxonEmissionContext(request, new(request.Profile));
    }

    void Because()
    {
        new AxonAutomationsEmitter().Emit(_context);
        InlineImplementationDiagnostics.Emit(_context);
    }

    void Destroy() => Directory.Delete(_directory, true);

    [Fact] void should_parse_a_slice_owned_requirement() => _requirement.Owner.Kind.ShouldEqual(SemanticKind.Slice);
    [Fact] void should_consume_the_requirement() => _context.ConsumedImplementations.Contains(_requirement.RequirementId).ShouldBeTrue();
    [Fact] void should_not_report_anything() => _context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    [Fact] void should_not_emit_an_unsupported_implementations_class() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/UnsupportedImplementations.java", StringComparison.Ordinal)).ShouldBeFalse();
}
