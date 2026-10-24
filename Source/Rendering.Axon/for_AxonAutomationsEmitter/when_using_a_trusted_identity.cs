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

    [Fact] void should_use_only_the_declared_roles() => Code("SubmitOrderReaction").ShouldContain("java.util.List.of(new org.springframework.security.core.authority.SimpleGrantedAuthority(\"ROLE_Sales\"))");
    [Fact] void should_not_grant_a_system_role_implicitly() => Code("SubmitOrderReaction").ShouldNotContain("ROLE_SYSTEM");
    [Fact] void should_install_a_fresh_context() => Code("SubmitOrderReaction").ShouldContain("SecurityContextHolder.createEmptyContext()");
    [Fact] void should_restore_the_previous_context_in_finally() => Code("SubmitOrderReaction").ShouldContain("finally {\n            org.springframework.security.core.context.SecurityContextHolder.setContext(previous);");
}
