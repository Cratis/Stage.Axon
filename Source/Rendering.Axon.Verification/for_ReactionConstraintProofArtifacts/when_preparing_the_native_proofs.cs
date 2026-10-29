// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ReactionConstraintProofArtifacts;

public class when_preparing_the_native_proofs : Specification
{
    IReadOnlyDictionary<string, string> _sources;

    void Because() => _sources = ReactionConstraintProofArtifacts.Sources();

    [Fact]
    void should_embed_the_shared_support_and_the_memory_proof() => _sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(
    [
        "GeneratedMemoryReactionConstraintTest.java", "GeneratedReactionConstraintTest.java", "GeneratedReactionProofSupport.java"
    ]).ShouldBeTrue();
    [Fact] void should_resolve_the_application_package() => _sources.Values.Any(source => source.Contains("__APPLICATION__", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_add_license_headers_to_java() => _sources.Values.Any(source => source.Contains("Copyright (c) Cratis", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_mix_with_the_ingress_templates() => IngressProofArtifacts.Sources().ContainsKey("GeneratedReactionConstraintTest.java").ShouldBeFalse();
    [Fact]
    void should_reject_missing_memory_proofs() => Catch.Exception(() => NativeTestResults.RequireReports(ReactionConstraintProofArtifacts.Application,
    [
        "TEST-corpusreactionconstraints.runtime.GeneratedReactionConstraintTest.xml"
    ])).ShouldBeOfExactType<NativeVerificationFailed>();
    [Fact]
    void should_accept_the_native_proof_report() => Catch.Exception(() => NativeTestResults.RequireReports(ReactionConstraintProofArtifacts.Application,
    [
        "TEST-corpusreactionconstraints.runtime.GeneratedMemoryReactionConstraintTest.xml"
    ])).ShouldBeNull();
}
