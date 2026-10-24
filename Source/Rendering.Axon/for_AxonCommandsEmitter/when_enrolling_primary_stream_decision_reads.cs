// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Decisions;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_enrolling_primary_stream_decision_reads : given.a_protected_decision_model
{
    AxonEmissionContext context;
    string stream;
    string handler;
    CommandDecisionReads otherKey;
    CommandDecisionReads generatedKey;

    void Because()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var located = slices.Single(slice => slice.Slice.Commands.Any(command => command.Name == "MoveOrder"));
        var seed = located.Slice.Commands[0];
        var key = seed.Properties.Single(property => property.IsIdentifier);
        var command = seed;
        context = new(request, new(request.Profile));
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        CommandStreamEmitter.Emit(context, located, command, source!);
        new CommandHandlerEmitter(context, located, command, source).Emit();
        stream = Code("MoveOrderStream");
        handler = Code("MoveOrderHandler");
        otherKey = new(context, command with { Reads = [command.Reads[0] with { Key = seed.Properties.Single(property => !property.IsIdentifier).Id }] }, source);
        generatedKey = new(context, command, source! with { Input = key with { IsGenerated = true } });
    }

    [Fact] void should_fold_the_required_view_presence_from_native_events() => stream.ShouldContain("decisionRead0 = true;");
    [Fact] void should_forget_the_view_on_its_removal_event() => stream.ShouldContain("decisionRead0 = false;");
    [Fact] void should_handle_the_view_input_event() => stream.ShouldContain(".OrderPlaced event)");
    [Fact] void should_overload_on_for_both_event_types() => stream.Split("public void on(", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_not_number_event_handlers() => stream.ShouldNotContain("public void on0(");
    [Fact] void should_expose_the_decision_state() => stream.ShouldContain("public boolean decisionRead0() { return decisionRead0; }");
    [Fact] void should_read_the_enrolled_stream_not_a_snapshot_store() => handler.ShouldContain("stream.entity().decisionRead0()");
    [Fact] void should_fail_before_appending_if_the_read_is_absent() => (handler.IndexOf("Required decision read 'order'", StringComparison.Ordinal) < handler.IndexOf("appender.append", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_fail_before_generated_values_or_rules() => (handler.IndexOf("Required decision read 'order'", StringComparison.Ordinal) < handler.IndexOf("rules.validate", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_reject_with_a_command_validation_error() => handler.ShouldContain("runtime.CommandValidationException");
    [Fact] void should_not_degrade_supported_presence_reads() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(0);
    [Fact] void should_refuse_cross_stream_decisions() => (otherKey.Prepare() is null).ShouldBeTrue();
    [Fact] void should_refuse_a_generated_read_key() => (generatedKey.Prepare() is null).ShouldBeTrue();

    string Code(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
