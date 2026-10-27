// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_NativeTestResults;

public class when_requiring_the_client_proof_reports : Specification
{
    [Fact]
    void should_accept_the_contract_report() =>
        Catch.Exception(() => NativeTestResults.RequireReports(ClientProofArtifacts.Application, ["target/surefire-reports/TEST-org.cratis.ClientContractTest.xml", "TEST-org.cratis.ClientLiveLimitsTest.xml"])).ShouldBeNull();

    [Theory]
    [InlineData("TEST-org.cratis.ClientLiveLimitsTest.xml")]
    [InlineData("TEST-org.cratis.NotClientContractTest.xml")]
    public void should_reject_a_missing_contract_report(string report) =>
        Catch.Exception(() => NativeTestResults.RequireReports(ClientProofArtifacts.Application, [report])).ShouldBeOfExactType<NativeVerificationFailed>();

    [Fact]
    void should_not_require_reports_of_other_applications() =>
        Catch.Exception(() => NativeTestResults.RequireReports("other", ["TEST-x.Other.xml"])).ShouldBeNull();
}
