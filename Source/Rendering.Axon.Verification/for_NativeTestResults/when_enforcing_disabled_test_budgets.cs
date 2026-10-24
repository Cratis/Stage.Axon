// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_NativeTestResults;

public class when_enforcing_disabled_test_budgets : Specification
{
    [Theory]
    [InlineData("STAGE-AXON-SPEC", 125, true)]
    [InlineData("STAGE-AXON-SPEC", 126, true)]
    [InlineData("STAGE-AXON-SPEC", 127, false)]
    [InlineData("STAGE-AXON-UNKNOWN", 1, false)]
    public void should_allow_only_known_reasons_within_their_measured_budget(string reason, int count, bool allowed)
    {
        var error = Catch.Exception(() => NativeTestResults.RequireWithinBudget(new Dictionary<string, int> { [reason] = count }, new Dictionary<string, int> { ["STAGE-AXON-SPEC"] = 126 }));
        (error is null).ShouldEqual(allowed);
    }
}
