// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandSpecificationsEmitter;

public class when_a_rejected_command_has_an_invalid_concept : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string rejected;
    string accepted;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var command = located.Slice.Commands[0];
        var specification = located.Slice.Specifications.First(candidate => candidate.When?.Command == command.Id);
        rejected = Render(located, command, specification with { ThenErrors = [new(null, "Quantity must be positive.")], ThenEvents = [] });
        accepted = Render(located, command, specification with { ThenErrors = [] });
    }

    [Fact] void should_construct_the_command_inside_the_rejection_boundary() => rejected.ShouldContain("catch (IllegalArgumentException error)");
    [Fact] void should_assert_the_constructor_rejection_message() => rejected.ShouldContain("assertThat(String.valueOf(error.getMessage())).contains(\"Quantity must be positive.\");");
    [Fact] void should_not_dispatch_when_construction_was_rejected() => rejected.ShouldContain("return;");
    [Fact] void should_still_dispatch_a_constructed_command() => rejected.ShouldContain("scenario.when().command(command");
    [Fact] void should_not_catch_construction_failures_of_accepted_commands() => accepted.ShouldNotContain("catch (IllegalArgumentException");

    string Render(LocatedSemanticSlice located, SemanticCommand command, SemanticSpecification specification)
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var slice = located with { Slice = located.Slice with { Specifications = [specification] } };
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandSpecificationsEmitter(context, slice, command, source, false).Emit();
        return Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SpecificationsTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }
}
