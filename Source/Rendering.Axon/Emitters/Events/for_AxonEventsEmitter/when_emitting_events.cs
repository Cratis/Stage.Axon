// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventsEmitter;

public class when_emitting_events : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(request, new AxonDegradation(request.Profile));
        new AxonEventsEmitter().Emit(context);
    }

    [Fact]
    void should_emit_events_as_records_in_the_module_package()
    {
        var artifact = context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/OrderPlaced.java", StringComparison.Ordinal));
        artifact.RelativePath.ShouldContain("/orders/events/");
        Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).ShouldContain("public record OrderPlaced(");
    }

    [Fact]
    void should_emit_the_axon_namespace_name_and_revision()
    {
        var code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/OrderPlaced.java", StringComparison.Ordinal)).Bytes.AsSpan());
        code.ShouldContain("@org.axonframework.messaging.eventhandling.annotation.Event(namespace = \"orders\", name = \"OrderPlaced\", version = \"1\")");
    }

    [Fact]
    void should_resolve_the_same_type_name_for_other_emitters()
    {
        var contract = ScopeSelection.Locate(loaded.Model.Application).SelectMany(slice => slice.Slice.Events).First(@event => @event.Name == "OrderPlaced");
        AxonEventsEmitter.TypeFor(context, contract.Id).ShouldEqual(context.BasePackage + ".orders.events.OrderPlaced");
    }
}
