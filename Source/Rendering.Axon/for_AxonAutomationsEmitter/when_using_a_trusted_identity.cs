// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_using_a_trusted_identity : given.a_reaction_model
{
    void Establish() => reaction = reaction with { RunsAs = new(SemanticReactionIdentityKind.System, ["Sales"]) };

    void Because() => Render();

    [Fact] void should_forward_the_declared_identity_instead_of_the_http_caller() => Code("SubmitOrderEndpoint").ShouldContain("metadata = Metadata.with(\"username\", \"system\").and(\"authorities\", \"ROLE_Sales\");");
    [Fact] void should_forward_native_metadata_on_the_gateway_call() => Code("SubmitOrderEndpoint").ShouldContain("Object.class, metadata, null);");
    [Fact] void should_use_only_the_declared_roles() => Code("SubmitOrderEndpoint").ShouldContain("List.of(new SimpleGrantedAuthority(\"ROLE_Sales\"))");
    [Fact] void should_not_grant_a_system_role_implicitly() => Code("SubmitOrderEndpoint").ShouldNotContain("ROLE_SYSTEM");
    [Fact] void should_install_a_fresh_context() => Code("SubmitOrderEndpoint").ShouldContain("SecurityContextHolder.createEmptyContext()");
    [Fact] void should_restore_the_previous_context_in_finally() => Code("SubmitOrderEndpoint").ShouldContain("finally {\n            SecurityContextHolder.setContext(previous);");
}
