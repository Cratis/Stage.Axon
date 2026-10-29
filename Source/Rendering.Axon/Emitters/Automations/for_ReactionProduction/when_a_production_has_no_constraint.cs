// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionProduction;

public class when_a_production_has_no_constraint : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string handler;

    void Because()
    {
        context = new AxonEmissionContext(request, new(request.Profile));
        var production = new ReactionProduction("AppendEvent", new("sample.orders.events.OrderPlaced", "unused", "unused", null), ("eventSourceId", "sample.concepts.OrderId", "eventSourceId"));
        production.Emit(context, "sample.reactions", request.Model.Application.Id);
        handler = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AppendEventHandler.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_append_directly() => handler.ShouldContain("appender.append(command.event());");
    [Fact] void should_not_allocate_an_event_list() => handler.ShouldNotContain("List.of(");
    [Fact] void should_not_load_a_stream() => handler.ShouldNotContain("Stream");
    [Fact] void should_not_inject_an_entity() => handler.ShouldNotContain("InjectEntity");
    [Fact] void should_not_emit_a_stream() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("Stream.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_use_a_unique_value_store() => handler.ShouldNotContain("uniqueValues");
    [Fact] void should_invoke_with_the_context_inside_an_event_handler() => new ReactionProduction("AppendEvent", new("sample.orders.events.OrderPlaced", "e", "s", null), ("eventSourceId", "sample.concepts.OrderId", "eventSourceId")).Invocation(true).ShouldContain("), context);");
    [Fact] void should_invoke_without_the_context_outside_an_event_handler() => new ReactionProduction("AppendEvent", new("sample.orders.events.OrderPlaced", "e", "s", null), ("eventSourceId", "sample.concepts.OrderId", "eventSourceId")).Invocation(false).ShouldNotContain("context");
    [Fact] void should_pass_a_null_context_with_metadata_outside_an_event_handler() => new ReactionProduction("AppendEvent", new("sample.orders.events.OrderPlaced", "e", "s", null), ("eventSourceId", "sample.concepts.OrderId", "eventSourceId")).InvocationWithMetadata(false).ShouldContain("metadata, null);");
    [Fact] void should_pass_the_context_with_metadata_inside_an_event_handler() => new ReactionProduction("AppendEvent", new("sample.orders.events.OrderPlaced", "e", "s", null), ("eventSourceId", "sample.concepts.OrderId", "eventSourceId")).InvocationWithMetadata(true).ShouldContain("metadata, context);");
}
