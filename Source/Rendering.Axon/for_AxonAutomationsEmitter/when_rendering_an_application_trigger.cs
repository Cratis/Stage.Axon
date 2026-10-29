// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_an_application_trigger : given.a_reaction_model
{
    void Because() => Render();

    [Fact] void should_register_the_endpoint() => Code("SubmitOrderEndpoint").ShouldContain("@RestController");
    [Fact] void should_not_emit_a_reaction_class() => context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/SubmitOrderReaction.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_send_the_command_from_a_private_effect_method() => Code("SubmitOrderEndpoint").ShouldContain("private void runSubmitOrder(SubmitOrder input, Metadata metadata)");
    [Fact] void should_not_inject_a_reaction_bean() => Code("SubmitOrderEndpoint").ShouldNotContain("reaction0");
    [Fact] void should_receive_the_http_authentication() => Code("SubmitOrderEndpoint").ShouldContain("SubmitOrder input, Authentication authentication)");
    [Fact] void should_pass_the_metadata_to_the_effect() => Code("SubmitOrderEndpoint").ShouldContain("runSubmitOrder(input, metadata);");
    [Fact] void should_forward_only_the_caller_name() => Code("SubmitOrderEndpoint").ShouldContain(".and(\"username\", authentication.getName())");
    [Fact] void should_forward_authority_names() => Code("SubmitOrderEndpoint").ShouldContain("map(GrantedAuthority::getAuthority).collect(Collectors.joining(\",\"))");
    [Fact] void should_dispatch_with_native_metadata() => Code("SubmitOrderEndpoint").ShouldContain("Object.class, metadata, null);");
    [Fact] void should_wait_for_the_invoked_command() => Code("SubmitOrderEndpoint").ShouldContain("commands.sendAndWait(new ");
    [Fact] void should_map_the_input_properties() => Code("SubmitOrderEndpoint").ShouldContain("input.customer()");
    [Fact] void should_emit_a_system_only_endpoint() => Code("SubmitOrderEndpoint").ShouldContain("@PreAuthorize(\"hasRole('SYSTEM')\")");
    [Fact] void should_use_a_kebab_route() => Code("SubmitOrderEndpoint").ShouldContain("/api/triggers/submit-order");
    [Fact] void should_validate_trigger_payloads() => Code("SubmitOrderEndpoint").ShouldContain("@Valid @RequestBody SubmitOrder input");
    [Fact] void should_not_report_unsupported_intent() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(0);
}
