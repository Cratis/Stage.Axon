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
    IReadOnlyDictionary<string, string> _persistent;
    string _all;

    void Because()
    {
        _sources = IngressProofArtifacts.Sources();
        _memory = IngressProofArtifacts.Sources(IngressProofModel.Application);
        _persistent = IngressProofArtifacts.Sources(IngressProofModel.PersistentApplication);
        _all = string.Join('\n', _sources.Values);
    }

    [Fact]
    void should_include_all_native_proof_sources() => _sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(
    [
        "GeneratedIngressSupport.java", "GeneratedMemoryProtectedReadTest.java", "GeneratedPersistentProtectedReadTest.java",
        "GeneratedPersistentUniqueConcurrencyTest.java", "GeneratedProtectedReadTest.java", "GeneratedSecurityIngressTest.java"
    ]).ShouldBeTrue();
    [Fact] void should_resolve_all_template_packages() => _all.ShouldNotContain("__APPLICATION__");
    [Fact] void should_resolve_the_runtime_package() => _all.ShouldNotContain("__RUNTIME__");
    [Fact] void should_not_add_license_headers_to_java() => _all.ShouldNotContain("Copyright (c) Cratis");
    [Fact] void should_drive_real_generated_commands() => _all.ShouldContain("gateway.send(command, Void.class)");
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
    [Fact] void should_run_the_persistent_protected_read_probe() => _sources["GeneratedPersistentProtectedReadTest.java"].ShouldContain("@ActiveProfiles(\"persistent\")");
    [Fact] void should_inherit_the_persistent_unique_races() => _sources["GeneratedPersistentUniqueConcurrencyTest.java"].ShouldContain("extends PersistentUniqueConcurrencyTest");
    [Fact] void should_check_actual_recorded_caller_identity() => _sources["GeneratedSecurityIngressTest.java"].ShouldContain("assertEquals(\"alice\", fact.actor())");
    [Fact] void should_not_define_an_unauthorized_http_status() => _all.ShouldNotContain("isUnauthorized()");
    [Fact] void should_not_define_a_forbidden_http_status() => _all.ShouldNotContain("isForbidden()");
    [Fact] void should_report_observed_denial_statuses() => _all.ShouldContain("response.getStatus()");
    [Fact] void should_not_start_the_scoped_app_with_the_persistent_profile() => _memory.Values.Any(source => source.Contains("@ActiveProfiles(\"persistent\")", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_keep_jpa_uniqueness_proofs_out_of_the_scoped_app() => _memory.ContainsKey("GeneratedPersistentUniqueConcurrencyTest.java").ShouldBeFalse();
    [Fact] void should_keep_jpa_read_proofs_out_of_the_scoped_app() => _memory.ContainsKey("GeneratedPersistentProtectedReadTest.java").ShouldBeFalse();
    [Fact] void should_run_jpa_uniqueness_in_the_viewless_app() => _persistent["GeneratedPersistentUniqueConcurrencyTest.java"].ShouldContain("@ActiveProfiles(\"persistent\")");
    [Fact] void should_run_jpa_protected_reads_in_the_viewless_app() => _persistent["GeneratedPersistentProtectedReadTest.java"].ShouldContain("@ActiveProfiles(\"persistent\")");
    [Fact] void should_bind_persistent_proofs_to_the_viewless_package() => _persistent.Values.All(source => source.Contains("package corpusingresspersistent.runtime;", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_require_view_types_in_the_persistent_app() => _persistent.Values.Any(source => source.Contains(".accounts.registry.active.", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_keep_authorization_proofs_in_the_scoped_app() => _memory.ContainsKey("GeneratedSecurityIngressTest.java").ShouldBeTrue();
    [Fact] void should_keep_memory_proofs_in_the_scoped_app() => _memory.ContainsKey("GeneratedMemoryProtectedReadTest.java").ShouldBeTrue();
}
