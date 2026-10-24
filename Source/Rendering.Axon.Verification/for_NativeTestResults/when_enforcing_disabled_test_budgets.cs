// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_NativeTestResults;

public class when_enforcing_disabled_test_budgets : Specification
{
    [Theory]
    [InlineData("STAGE-AXON-0501", 62, false)]
    [InlineData("STAGE-AXON-0501", 63, true)]
    [InlineData("STAGE-AXON-0501", 64, false)]
    [InlineData("STAGE-AXON-UNKNOWN", 1, false)]
    [InlineData("System property [stage.restart] does not exist", 1, false)]
    [InlineData("STAGE-AXON-0501", 0, false)]
    public void should_allow_only_known_reasons_within_their_measured_budget(string reason, int count, bool allowed)
    {
        var error = Catch.Exception(() => NativeTestResults.RequireWithinBudget(new Dictionary<string, int> { [reason] = count }, new Dictionary<string, DisabledTestBudget> { ["STAGE-AXON-0501"] = new(63, "Unsupported view intent; remove after lowering it.") }));
        (error is null).ShouldEqual(allowed);
    }

    [Fact]
    void should_reject_a_reason_that_no_longer_occurs() =>
        Catch.Exception(() => NativeTestResults.RequireWithinBudget(new Dictionary<string, int>(), new Dictionary<string, DisabledTestBudget> { ["STAGE-AXON-0501"] = new(1, "Unsupported view intent") })).ShouldBeOfExactType<NativeVerificationFailed>();

    [Theory]
    [InlineData(0, "Unused entry")]
    [InlineData(1, "")]
    public void should_require_positive_measured_counts_and_an_explanation(int count, string reason) =>
        Catch.Exception(() => NativeTestResults.RequireWithinBudget(new Dictionary<string, int> { ["STAGE-AXON-0501"] = count }, new Dictionary<string, DisabledTestBudget> { ["STAGE-AXON-0501"] = new(count, reason) })).ShouldBeOfExactType<NativeVerificationFailed>();
}
