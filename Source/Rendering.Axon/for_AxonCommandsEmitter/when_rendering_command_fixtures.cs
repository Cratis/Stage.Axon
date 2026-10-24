// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_command_fixtures : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    LocatedSemanticSlice located;
    SemanticCommand command;
    SemanticSpecification specification;
    string denial;
    string rejection;
    string replay;
    string coded;

    void Establish()
    {
        located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        command = located.Slice.Commands[0];
        specification = located.Slice.Specifications[0];
    }

    void Because()
    {
        denial = Render(specification with { ThenDenied = true, ThenEvents = [] });
        rejection = Render(specification with { ThenErrors = [new(null, "Not allowed.")], ThenEvents = [] });
        replay = Render(specification with { GivenEvents = specification.ThenEvents, GivenClock = "2030-01-02T03:04:05Z" });
        coded = Render(specification with { ThenErrors = [new("Required", "Not allowed.")], ThenEvents = [] });
    }

    [Fact] void should_check_denial_without_events() => denial.ShouldContain("result.exception(org.springframework.security.access.AccessDeniedException.class).noEvents();");
    [Fact] void should_enforce_the_policy_during_denial_tests() => denial.ShouldContain("AuthorizationManagerBeforeMethodInterceptor.preAuthorize()");
    [Fact] void should_assert_the_typed_rejection() => rejection.ShouldContain(".runtime.CommandValidationException.class);");
    [Fact] void should_assert_rejection_messages() => rejection.ShouldContain(".contains(\"Not allowed.\");");
    [Fact] void should_assert_no_append_on_rejection() => rejection.ShouldContain("result.noEvents();");
    [Fact] void should_load_the_given_history() => replay.ShouldContain(".given().events(new ");
    [Fact] void should_use_the_modeled_clock() => replay.ShouldContain("fixture(\"2030-01-02T03:04:05Z\")");
    [Fact] void should_explicitly_disable_unsupported_error_codes() => coded.ShouldContain("@Disabled(\"STAGE-AXON-0112:");
    [Fact] void should_not_turn_unsupported_error_codes_into_a_passing_stub() => coded.ShouldContain("throw new UnsupportedOperationException(");

    string Render(SemanticSpecification candidate)
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var slice = located with { Slice = located.Slice with { Specifications = [candidate] } };
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        var usesClock = new CommandHandlerEmitter(context, slice, command, source).Emit();
        new CommandSpecificationsEmitter(context, slice, command, source, usesClock).Emit();
        return Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SpecificationsTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }
}
