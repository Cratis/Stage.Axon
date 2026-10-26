// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionProduction;

public class when_importing_stream_tag_constants : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string handler;
    string stream;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var production = new ReactionProduction("AppendEvent", new("sample.orders.events.OrderPlaced", "unused", "unused", null), ("eventSourceId", "sample.concepts.OrderId", "eventSourceId"), "sample.orders.events.EventTags.EVENT_SOURCE_ID");
        production.Emit(context, "sample.reactions", request.Model.Application.Id);
        handler = Code("AppendEventHandler");
        stream = Code("AppendEventStream");
        string Code(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_import_the_handler_tag_owner() => handler.ShouldContain("import sample.orders.events.EventTags;");
    [Fact] void should_select_the_existing_command_property() => handler.ShouldContain("@InjectEntity(idProperty = EventTags.EVENT_SOURCE_ID) @Nullable AppendEventStream stream");
    [Fact] void should_import_the_stream_tag_owner() => stream.ShouldContain("import sample.orders.events.EventTags;");
    [Fact] void should_use_the_constant_in_stream_annotations() => stream.ShouldContain("tagKey = EventTags.EVENT_SOURCE_ID");
    [Fact] void should_append_directly() => handler.ShouldContain("appender.append(command.event());");
    [Fact] void should_not_allocate_an_event_list() => handler.ShouldNotContain("List.of(");
}
