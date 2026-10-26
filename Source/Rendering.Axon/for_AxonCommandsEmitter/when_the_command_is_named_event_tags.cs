// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_the_command_is_named_event_tags : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string handler;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => slice.Slice.Commands.Any(candidate => !candidate.Produces.IsEmpty));
        var command = located.Slice.Commands.First(candidate => !candidate.Produces.IsEmpty) with { Name = "EventTags" };
        var context = new AxonEmissionContext(request, new(request.Profile));
        var source = CommandSource.Create(context, command, CommandAppends.Contract(context, command.Produces[0].EventContract));
        new CommandHandlerEmitter(context, located, command, source).Emit();
        handler = Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
    }

    [Fact] void should_keep_the_command_name_short() => handler.ShouldContain("handle(EventTags command,");
    [Fact] void should_qualify_the_colliding_tag_owner() => handler.ShouldContain("@InjectEntity(idProperty = sample.orders.events.EventTags.ORDER_ID)");
    [Fact] void should_not_import_the_colliding_tag_owner() => handler.ShouldNotContain("import sample.orders.events.EventTags;");
}
