// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_rendering_an_application_trigger_specification : given.a_reaction_model
{
    string code;

    void Establish() => Specify();

    void Because()
    {
        Render();
        code = Code("SubmitOrderFiringSpecificationsTest");
    }

    [Fact] void should_use_the_native_application() => code.ShouldContain("@SpringBootTest");
    [Fact] void should_inject_the_real_reaction() => code.ShouldContain("@Autowired private SubmitOrderReaction reaction;");
    [Fact] void should_fire_the_typed_application_trigger() => code.ShouldContain("reaction.on0(new SubmitOrder(");
    [Fact] void should_pin_the_injected_clock() => code.ShouldContain("Mockito.when(clock.instant())");
    [Fact] void should_assert_the_complete_native_append_set() => code.ShouldContain("Assertions.assertThat(actual).containsExactly(new OrderPlaced(");
    [Fact] void should_exclude_storage_consistency_markers_from_facts() => code.ShouldContain("instanceof TerminalEventMessage");
    [Fact] void should_isolate_application_state() => code.ShouldContain("@DirtiesContext");
    [Fact] void should_correlate_the_junit_display_name() => code.ShouldContain(specification.Id.ToString());
    [Fact] void should_claim_the_model_specification() => context.Specifications.ContainsKey(specification.Id).ShouldBeTrue();
    [Fact] void should_not_disable_supported_fixtures() => code.ShouldNotContain("@Disabled");
    [Fact] void should_import_the_caller_security_types() => code.ShouldContain("import org.springframework.security.core.context.SecurityContextHolder;");
    [Fact] void should_use_the_short_caller_security_names() => code.ShouldContain("SecurityContextHolder.setContext(SecurityContextHolder.createEmptyContext());");
    [Fact] void should_not_qualify_the_caller_security_types() => code.ShouldNotContain("org.springframework.security.core.context.SecurityContextHolder.");
    [Fact] void should_keep_the_anonymous_role_literal() => code.ShouldContain("\"ROLE_ANONYMOUS\"");
    [Fact] void should_import_list() => code.ShouldContain("import java.util.List;");
}
