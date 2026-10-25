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

public class when_rendering_conditional_appends : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string handler;
    string multiple;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var property = seed.Properties.Single(property => property.Name == "customer");
        var conditional = seed.Produces[0] with { When = new SemanticComparison(new(property.Id, null), SemanticComparisonOperator.NotEqual, new(default, new SemanticTextValue("blocked"))) };
        handler = Render(seed with { Produces = [conditional] });
        multiple = Render(seed with { Produces = [seed.Produces[0], seed.Produces[0]] });

        string Render(SemanticCommand command)
        {
            var context = new AxonEmissionContext(request, new(request.Profile));
            var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
            new CommandHandlerEmitter(context, located, command, source).Emit();
            return Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PlaceOrderHandler.java", StringComparison.Ordinal)).Bytes.AsSpan());
        }
    }

    [Fact] void should_collect_conditional_events() => handler.ShouldContain("var events = new java.util.ArrayList<Object>();");
    [Fact] void should_guard_the_event_with_the_modeled_condition() => handler.ShouldContain("if (!(java.util.Objects.equals((command.customer() == null ? null : command.customer().value()), \"blocked\")))");
    [Fact] void should_add_the_event_inside_the_condition() => handler.ShouldContain("events.add(new ");
    [Fact] void should_not_append_an_empty_batch() => handler.ShouldContain("if (!events.isEmpty()) appender.append(events);");
    [Fact] void should_keep_multiple_events_as_a_batch() => multiple.ShouldContain("if (!events.isEmpty()) appender.append(events);");
}
