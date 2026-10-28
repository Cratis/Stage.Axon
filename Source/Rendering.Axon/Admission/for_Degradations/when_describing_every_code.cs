// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_Degradations;

public class when_describing_every_code
{
    [Fact]
    public void should_describe_every_distinct_constant_once()
    {
        var codes = typeof(AxonRefusalCodes).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()).Distinct(StringComparer.Ordinal);
        foreach (var code in codes) Assert.Single(Degradations.All, row => row.Code == code);
        Assert.Equal(Degradations.All.Count, Degradations.All.Select(row => row.Code).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void should_describe_the_intent_and_reason_of_every_code()
    {
        Assert.All(Degradations.All, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Construct));
            Assert.False(string.IsNullOrWhiteSpace(row.Reason));
            Assert.NotEqual(row.Construct, row.Reason);
        });
    }

    [Fact] public void should_tolerate_unknown_codes() => Assert.Null(Degradations.Find("STAGE-AXON-UNKNOWN"));

    [Theory]
    [InlineData("STAGE-AXON-GENERATION")]
    [InlineData("STAGE-AXON-0307")]
    [InlineData("STAGE-AXON-NAMES")]
    [InlineData("STAGE-AXON-SUPPORT-PACKAGE")]
    public void should_document_the_s1_warnings(string code) => Assert.Equal(ArtifactRenderDiagnosticSeverity.Warning, Degradations.Find(code).Severity);

    [Theory]
    [InlineData("STAGE-AXON-VERSION")]
    [InlineData("STAGE-AXON-PROFILE")]
    [InlineData("STAGE-AXON-SCOPE")]
    [InlineData("STAGE-AXON-GAP-MODE")]
    [InlineData("STAGE-AXON-REFUSAL-MODE")]
    [InlineData("STAGE-AXON-LOCAL-RUNTIME")]
    public void should_document_admission_and_mode_codes_as_informational(string code) => Assert.Equal(DegradationKind.Informational, Degradations.Find(code).Kind);
}
