// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_IngressProofArtifacts;

public partial class when_preparing_native_proofs : Specification
{
    [GeneratedRegex(@"\b(java|javax|jakarta|org\.axonframework|org\.springframework)\.[a-z]+\.[A-Za-z.]*[A-Z]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex FrameworkType { get; }

    IReadOnlyDictionary<string, string> _sources;
    IReadOnlyDictionary<string, string> _memory;
    string _all;

    void Because()
    {
        _sources = IngressProofArtifacts.Sources();
        _memory = IngressProofArtifacts.Sources(IngressProofModel.Application);
        _all = string.Join('\n', _sources.Values);
    }

    [Fact]
    void should_include_all_native_proof_sources() => _sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(
    [
        "GeneratedDemoIdentityTest.java", "GeneratedIngressSupport.java", "GeneratedInlinePolicyDemoTest.java", "GeneratedInlinePolicyPerformerTest.java", "GeneratedInlinePolicyTest.java", "GeneratedJwtAudienceTest.java", "GeneratedMemoryProtectedReadTest.java", "GeneratedNameOnlyMetadataTest.java",
        "GeneratedProtectedReadTest.java", "GeneratedSecurityIngressTest.java"
    ]).ShouldBeTrue();
    [Fact]
    void should_prove_basic_identity_fails_closed_and_application_providers_win()
    {
        var source = ClientProofArtifacts.JavaSources()["GeneratedBasicIdentityTest.java"];
        source.ShouldContain("bootDefaultUserCannotAuthenticateWithConfiguredOrArbitraryPassword");
        source.ShouldContain("applicationIdentityProviderWinsWhileNativeAuthorizationStillDeniesWrongRole");
        source.ShouldContain(".andExpect(status().isUnauthorized())");
        source.ShouldContain(".andExpect(status().isForbidden())");
    }

    [Fact]
    void should_include_the_inline_policy_failure_and_composition_proofs()
    {
        var sources = IngressProofArtifacts.Sources("inline-java-policies");
        sources.Count.ShouldEqual(3);
        sources["GeneratedInlinePolicyTest.java"].ShouldContain("nativePolicyBeanReadsOnlyJwtAttributes");
        sources["GeneratedInlinePolicyTest.java"].ShouldContain("compositionsDenyTheWholeRealCommand");
        sources["GeneratedInlinePolicyTest.java"].ShouldContain("handlerClaimsAndUnprovenSubscriptionUpdatesFailClosed");
        sources["GeneratedInlinePolicyTest.java"].ShouldContain("\"Finance Operations\", \"Other\", \"throw\", \"error\"");
        sources["GeneratedInlinePolicyTest.java"].ShouldContain("\"and-snapshot\", \"or-snapshot\", \"escape-and-snapshot\", \"escape-or-snapshot\"");
        sources["GeneratedInlinePolicyDemoTest.java"].ShouldContain("stage.axon.demo.claims.department=Finance Operations");
    }

    [Fact]
    void should_prove_native_decoder_audiences_without_mocking()
    {
        var source = IngressProofArtifacts.Sources("inline-java-policies")["GeneratedJwtAudienceTest.java"];
        source.ShouldContain("wrongAudienceIsRejectedByTheRealDecoderAndApplication");
        source.ShouldContain("rightAudienceIsAuthenticatedBeforeTheNativePolicyDenial");
        source.ShouldContain("noConfiguredAudienceRejectsAnOtherwiseValidToken");
        source.ShouldContain("assertInstanceOf(NimbusJwtDecoder.class, decoder)");
        source.ShouldContain("new RSASSASigner(KEY.getPrivate())");
        source.ShouldNotContain("@Mock");
    }

    [Fact]
    void should_include_the_inline_policy_performed_query_proofs()
    {
        var sources = IngressProofArtifacts.Sources("inline-java-policy-performers");
        sources.Count.ShouldEqual(1);
        sources["GeneratedInlinePolicyPerformerTest.java"].ShouldContain("inlinePoliciesProtectPerformedQueriesOverHttp");
        sources["GeneratedInlinePolicyPerformerTest.java"].ShouldContain("inlinePoliciesProtectPerformedQueriesOverSse");
    }

    [Fact]
    void should_send_the_generated_inline_policy_command_field()
    {
        var sources = IngressProofArtifacts.Sources("inline-java-policies");
        foreach (var name in new[] { "GeneratedInlinePolicyTest.java", "GeneratedInlinePolicyDemoTest.java" })
        {
            var source = sources[name];
            source.ShouldContain("\\\"id\\\":\\\"");
            source.ShouldNotContain("\\\"accountId\\\":");
        }
    }

    [Fact]
    void should_require_denial_for_unproven_inline_policy_commands()
    {
        var sources = IngressProofArtifacts.Sources("inline-java-policies");
        sources["GeneratedInlinePolicyTest.java"].ShouldContain(".with(jwt().jwt(token -> token.claim(\"department\", department)))).andExpect(status().isForbidden())");
        sources["GeneratedInlinePolicyDemoTest.java"].ShouldContain(".andExpect(status().isUnauthorized())");
    }

    [Fact]
    void should_assert_synchronous_inline_policy_query_denials_directly()
    {
        var source = IngressProofArtifacts.Sources("inline-java-policies")["GeneratedInlinePolicyTest.java"];
        source.ShouldContain("return response.getRequest().isAsyncStarted() ? http.perform(asyncDispatch(response)) : result;");
        source.ShouldContain(".andExpect(status().isUnauthorized())");
    }

    [Fact] void should_prove_system_trigger_authorization_and_the_real_caller() => _sources["GeneratedNameOnlyMetadataTest.java"].ShouldContain("systemTriggerForwardsTheRealCallerThroughNativeAuthorization");
    [Fact] void should_wire_the_name_only_proof_into_the_ingress_app() => _memory.ContainsKey("GeneratedNameOnlyMetadataTest.java").ShouldBeTrue();
    [Fact] void should_prove_real_principals_and_stored_metadata() => _sources["GeneratedNameOnlyMetadataTest.java"].ShouldContain("basicAndJwtCommandsAndStoredEventsContainOnlyNamesNotPrincipalsOrCredentials");
    [Fact] void should_prove_denied_subscription_opening() => _sources["GeneratedNameOnlyMetadataTest.java"].ShouldContain("basicAndJwtWrongRolesDenyCommandsQueriesAndSubscriptionOpeningWithoutHandlerExecution");
    [Fact] void should_resolve_all_template_packages() => _all.ShouldNotContain("__APPLICATION__");
    [Fact] void should_resolve_the_runtime_package() => _all.ShouldNotContain("__RUNTIME__");
    [Fact] void should_resolve_the_runtime_module_package() => _all.ShouldNotContain("__RUNTIME_MODULE__");
    [Fact] void should_not_add_license_headers_to_java() => _all.ShouldNotContain("Copyright (c) Cratis");
    [Fact] void should_drive_real_generated_commands() => _all.ShouldContain("gateway.send(command, Void.class, metadata(\"alice\", \"Writer\"), null)");
    [Fact] void should_collect_event_messages_directly() => _sources["GeneratedIngressSupport.java"].ShouldContain(".collect(ArrayList<EventMessage>::new, (all, entry) -> all.add(entry))");
    [Fact] void should_filter_terminal_entries_before_collecting_messages() => _sources["GeneratedIngressSupport.java"].ShouldContain("stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))");
    [Fact] void should_never_append_directly_from_proofs() => _all.ShouldNotContain("appender.append(");
    [Fact] void should_never_construct_append_units_of_work() => _all.ShouldNotContain("executeWithResult(");
    [Fact] void should_use_bounded_native_contenders() => _all.ShouldContain("observed.await(5, TimeUnit.SECONDS)");
    [Fact] void should_not_sleep_in_native_proofs() => _all.ShouldNotContain("Thread.sleep(");
    [Fact]
    void should_use_imports_for_framework_types() => _all.Split('\n')
        .Any(line => !line.StartsWith("import ", StringComparison.Ordinal) && !line.StartsWith("package ", StringComparison.Ordinal) && FrameworkType.IsMatch(line)).ShouldBeFalse();
    [Fact] void should_run_the_memory_protected_read_probe() => _sources["GeneratedMemoryProtectedReadTest.java"].ShouldContain("extends GeneratedProtectedReadTest");
    [Fact] void should_check_actual_recorded_caller_identity() => _sources["GeneratedSecurityIngressTest.java"].ShouldContain("assertEquals(\"alice\", fact.actor())");
    [Fact] void should_wait_for_the_authorized_http_query() => _sources["GeneratedSecurityIngressTest.java"].ShouldContain(".andExpect(request().asyncStarted()).andReturn();\n        response.getAsyncResult(10000);");
    [Fact] void should_assert_the_dispatched_http_query_body() => _sources["GeneratedSecurityIngressTest.java"].ShouldContain("http.perform(asyncDispatch(response))\n                .andExpect(status().is2xxSuccessful()).andExpect(jsonPath(\"$.alias\").value(\"seeded\"))");
    [Fact] void should_keep_the_authorized_gateway_query_assertion() => _sources["GeneratedSecurityIngressTest.java"].ShouldContain("assertEquals(List.of(expected), result.rows())");
    [Fact] void should_assert_native_unauthorized_http_status() => _all.ShouldContain("status().isUnauthorized()");
    [Fact] void should_assert_native_forbidden_http_status() => _all.ShouldContain("status().isForbidden()");
    [Fact] void should_report_observed_denial_statuses() => _all.ShouldContain("response.getStatus()");
    [Fact] void should_not_activate_a_persistent_profile_in_any_proof() => _sources.Values.Any(source => source.Contains("@ActiveProfiles(\"persistent\")", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_keep_authorization_proofs_in_the_scoped_app() => _memory.ContainsKey("GeneratedSecurityIngressTest.java").ShouldBeTrue();
    [Fact] void should_keep_memory_proofs_in_the_scoped_app() => _memory.ContainsKey("GeneratedMemoryProtectedReadTest.java").ShouldBeTrue();
}
