// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_NativeTestResults;

public class when_requiring_the_constraint_proof_reports : Specification
{
    [Fact]
    void should_accept_the_in_memory_proof_report() =>
        Catch.Exception(() => NativeTestResults.RequireReports(ConstraintProofArtifacts.Application, ["target/surefire-reports/TEST-corpusconstrainedjavabodies.runtime.GeneratedNativeUniqueTest.xml"])).ShouldBeNull();

    [Theory]
    [InlineData("TEST-corpusconstrainedjavabodies.runtime.UniqueAppendTransactionTest.xml")]
    [InlineData("TEST-corpusconstrainedjavabodies.runtime.NotGeneratedNativeUniqueTest.xml")]
    public void should_reject_a_missing_in_memory_proof_report(string report) =>
        Catch.Exception(() => NativeTestResults.RequireReports(ConstraintProofArtifacts.Application, [report])).ShouldBeOfExactType<NativeVerificationFailed>();
}
