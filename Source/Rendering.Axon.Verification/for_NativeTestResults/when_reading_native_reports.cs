// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_NativeTestResults;

public class when_reading_native_reports : Specification
{
    IReadOnlyDictionary<string, int> _disabled;

    void Because() => _disabled = NativeTestResults.Disabled([XDocument.Parse("""
        <testsuite>
          <testcase name="executed" />
          <testcase name="first"><skipped message="STAGE-AXON-SPEC: first unsupported intent" /></testcase>
          <testcase name="second"><skipped message="STAGE-AXON-SPEC: second unsupported intent" /></testcase>
          <testcase name="view"><skipped message="STAGE-AXON-0501" /></testcase>
        </testsuite>
        """)]);

    [Fact] void should_group_disabled_tests_by_reason() => _disabled["STAGE-AXON-SPEC"].ShouldEqual(2);
    [Fact] void should_keep_distinct_reason_codes() => _disabled["STAGE-AXON-0501"].ShouldEqual(1);
    [Fact] void should_not_count_executed_tests_as_disabled() => _disabled.Values.Sum().ShouldEqual(3);
    [Theory]
    [InlineData("<testsuite />")]
    [InlineData("<testsuite><testcase><skipped message='STAGE-AXON-SPEC' /></testcase></testsuite>")]
    [InlineData("<testsuite><testcase><failure /></testcase></testsuite>")]
    [InlineData("<testsuite><testcase /><testcase><error /></testcase></testsuite>")]
    public void should_reject_unexecuted_or_failed_native_suites(string report) =>
        Catch.Exception(() => NativeTestResults.Disabled([XDocument.Parse(report)])).ShouldBeOfExactType<NativeVerificationFailed>();
}
