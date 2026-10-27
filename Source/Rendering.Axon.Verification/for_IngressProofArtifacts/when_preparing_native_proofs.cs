// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_IngressProofArtifacts;

public class when_preparing_native_proofs : Specification
{
    IReadOnlyDictionary<string, string> _sources;
    string _all;

    void Because()
    {
        _sources = IngressProofArtifacts.Sources();
        _all = string.Join('\n', _sources.Values);
    }

    [Fact] void should_include_all_native_proof_sources() => _sources.Count.ShouldEqual(6);
    [Fact] void should_resolve_all_template_packages() => _all.ShouldNotContain("__APPLICATION__");
    [Fact] void should_resolve_the_runtime_package() => _all.ShouldNotContain("__RUNTIME__");
    [Fact] void should_not_add_license_headers_to_java() => _all.ShouldNotContain("Copyright (c) Cratis");
    [Fact] void should_drive_real_generated_commands() => _all.ShouldContain("gateway.send(command, Void.class)");
    [Fact] void should_never_append_directly_from_proofs() => _all.ShouldNotContain("appender.append(");
    [Fact] void should_never_construct_append_units_of_work() => _all.ShouldNotContain("executeWithResult(");
    [Fact] void should_use_bounded_native_contenders() => _all.ShouldContain("observed.await(5, TimeUnit.SECONDS)");
    [Fact] void should_not_sleep_in_native_proofs() => _all.ShouldNotContain("Thread.sleep(");
    [Fact] void should_run_the_memory_protected_read_probe() => _sources["GeneratedMemoryProtectedReadTest.java"].ShouldContain("extends GeneratedProtectedReadTest");
    [Fact] void should_run_the_persistent_protected_read_probe() => _sources["GeneratedPersistentProtectedReadTest.java"].ShouldContain("@ActiveProfiles(\"persistent\")");
    [Fact] void should_inherit_the_persistent_unique_races() => _sources["GeneratedPersistentUniqueConcurrencyTest.java"].ShouldContain("extends PersistentUniqueConcurrencyTest");
    [Fact] void should_check_actual_recorded_caller_identity() => _sources["GeneratedSecurityIngressTest.java"].ShouldContain("assertEquals(\"alice\", fact.actor())");
    [Fact] void should_not_define_an_unauthorized_http_status() => _all.ShouldNotContain("isUnauthorized()");
    [Fact] void should_not_define_a_forbidden_http_status() => _all.ShouldNotContain("isForbidden()");
    [Fact] void should_report_observed_denial_statuses() => _all.ShouldContain("response.getStatus()");
}
