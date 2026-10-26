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
    [Fact] void should_inject_the_primary_stream_state() => Code("PlaceOrderHandler").ShouldContain("@InjectEntity(idProperty = EventTags.ORDER_ID) @Nullable PlaceOrderStream state");
    [Fact] void should_import_entity_injection() => Code("PlaceOrderHandler").ShouldContain("import org.axonframework.modelling.annotation.InjectEntity;");
    [Fact] void should_allow_absent_entity_snapshots_for_first_appends() => Code("PlaceOrderHandler").ShouldContain("import org.jspecify.annotations.Nullable;");
    [Fact] void should_import_the_tag_owner() => Code("PlaceOrderHandler").ShouldContain("import sample.orders.events.EventTags;");
    [Fact] void should_not_wrap_state_in_optional() => Code("PlaceOrderHandler").ShouldNotContain("Optional<");
    [Fact] void should_not_wrap_the_state() => Code("PlaceOrderHandler").ShouldNotContain("ManagedEntity");
    [Fact] void should_append_the_single_event_directly() => Code("PlaceOrderHandler").ShouldContain("appender.append(new ");
    [Fact] void should_not_collect_a_single_event() => Code("PlaceOrderHandler").ShouldNotContain("ArrayList");
    [Fact] void should_use_the_shared_primary_tag() => Code("PlaceOrderStream").ShouldContain("@EventSourcedEntity(tagKey = EventTags.ORDER_ID)");
    [Fact] void should_mark_the_command_identity() => Code("PlaceOrder").ShouldContain("@TargetEntityId");
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
