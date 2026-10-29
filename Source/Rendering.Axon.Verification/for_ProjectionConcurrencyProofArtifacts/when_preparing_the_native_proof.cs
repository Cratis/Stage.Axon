// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_ProjectionConcurrencyProofArtifacts;

public class when_preparing_the_native_proof : Specification
{
    IReadOnlyDictionary<string, string> _sources;

    void Because() => _sources = ProjectionConcurrencyProofArtifacts.Sources();

    [Fact] void should_embed_the_shared_key_proof() => _sources.Keys.Single().ShouldEqual("ConcurrentSharedKeyTest.java");
    [Fact] void should_resolve_the_application_package() => _sources.Values.Single().ShouldContain("package corpusdecimalarithmetic.pricing.basket.totals;");
    [Fact] void should_reject_a_missing_report() => Catch.Exception(() => NativeTestResults.RequireReports(ProjectionConcurrencyProofArtifacts.Application, ["TEST-other.xml"])).ShouldBeOfExactType<NativeVerificationFailed>();
    [Fact] void should_accept_the_proof_report() => Catch.Exception(() => NativeTestResults.RequireReports(ProjectionConcurrencyProofArtifacts.Application, ["TEST-corpusdecimalarithmetic.pricing.basket.totals.ConcurrentSharedKeyTest.xml"])).ShouldBeNull();
}
