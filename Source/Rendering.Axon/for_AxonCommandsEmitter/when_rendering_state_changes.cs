// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_state_changes : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Establish()
    {
        var selected = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        request = request with { Scope = new(ArtifactRenderScopeKind.Slice, selected.Slice.Id) };
        context = new(request, new(request.Profile));
    }

    void Because() => new AxonCommandsEmitter().Emit(context);

    [Fact] void should_enable_command_emission() => new AxonCommandsEmitter().IsImplemented.ShouldBeTrue();
    [Fact] void should_load_the_primary_stream_before_appending() => Code("PlaceOrderHandler").ShouldContain($"ManagedEntity<{context.BasePackage}.concepts.OrderId, PlaceOrderStream> stream");
    [Fact] void should_append_as_one_batch() => Code("PlaceOrderHandler").ShouldContain("appender.append(events)");
    [Fact] void should_use_the_shared_primary_tag() => Code("PlaceOrderStream").ShouldContain("@EventSourcedEntity(tagKey = \"orderId\")");
    [Fact] void should_mark_the_command_identity() => Code("PlaceOrder").ShouldContain("@org.axonframework.modelling.annotation.TargetEntityId");
    [Fact] void should_emit_a_post_endpoint() => Code("PlaceOrderEndpoint").ShouldContain("@PostMapping(\"/api/orders/orders/place-order\")");
    [Fact] void should_return_a_typed_result() => Code("PlaceOrderEndpoint").ShouldContain("CompletableFuture<Void>");
    [Fact] void should_dispatch_with_the_typed_result() => Code("PlaceOrderEndpoint").ShouldContain("send(command, Void.class)");
    [Fact] void should_not_return_an_untyped_result() => Code("PlaceOrderEndpoint").ShouldNotContain("<Object>");
    [Fact] void should_validate_requests_before_dispatch() => Code("PlaceOrderEndpoint").ShouldContain("rules.validateInput(command);");
    [Fact] void should_emit_real_axon_fixtures() => Code("PlaceOrderSpecificationsTest").ShouldContain("AxonTestFixture.with(configuration)");
    [Fact] void should_enforce_method_security_in_fixtures() => Code("PlaceOrderSpecificationsTest").ShouldContain("AuthorizationManagerBeforeMethodInterceptor.preAuthorize()");
    [Fact] void should_compare_the_expected_events() => Code("PlaceOrderSpecificationsTest").ShouldContain($"result.events(new {context.BasePackage}.orders.events.OrderPlaced(");
    [Fact] void should_supply_enum_constants() => Code("PlaceOrderSpecificationsTest").ShouldContain($"{context.BasePackage}.concepts.OrderChannel.WEB");
    [Fact] void should_not_disable_the_supported_command_specification() => Code("PlaceOrderSpecificationsTest").ShouldNotContain("@Disabled(");

    string Code(string type) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith('/' + type + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
