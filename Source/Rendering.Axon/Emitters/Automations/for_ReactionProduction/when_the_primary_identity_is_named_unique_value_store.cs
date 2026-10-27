// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionProduction;

public class when_the_primary_identity_is_named_unique_value_store : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _handler;

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var production = new ReactionProduction(
            "AppendProducedEvent",
            new(context.BasePackage + ".orders.events.OrderPlaced", "unused", "unused", null),
            ("uniqueValueStore", context.BasePackage + ".concepts.UniqueValueStore", "uniqueValueStore"),
            context.BasePackage + ".orders.events.EventTags.UNIQUE_VALUE_STORE");
        production.Emit(context, context.BasePackage + ".reactions", request.Model.Application.Id);
        _handler = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AppendProducedEventHandler.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_not_import_the_unused_application_identity() => _handler.ShouldNotContain("import sample.concepts.UniqueValueStore;");
    [Fact] void should_inject_a_nullable_snapshot() => _handler.ShouldContain("@InjectEntity @Nullable AppendProducedEventStream stream");
    [Fact] void should_not_wrap_the_snapshot() => _handler.ShouldNotContain("ManagedEntity");
    [Fact] void should_not_select_a_nonexistent_command_property() => _handler.ShouldNotContain("idProperty =");
    [Fact] void should_append_one_event_directly() => _handler.ShouldContain("appender.append(command.event());");
    [Fact] void should_use_the_short_runtime_store_name() => _handler.ShouldContain("private final UniqueValueStore uniqueValues;");
    [Fact] void should_use_the_short_runtime_constructor_parameter() => _handler.ShouldContain("AppendProducedEventHandler(UniqueValueStore uniqueValues)");
    [Fact] void should_import_the_runtime_store() => _handler.ShouldContain("import io.cratis.stage.axon.runtime.UniqueValueStore;");
}
