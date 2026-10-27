// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_an_invalid_java_package : given.a_facade_model
{
    [Theory]
    [InlineData("Acme..Orders")]
    [InlineData(".acme")]
    [InlineData("io.cratis.demo")]
    [InlineData("Org.SpringFramework.X")]
    [InlineData("java")]
    [InlineData("123.orders")]
    [InlineData("acme.123")]
    [InlineData("acme..orders")]
    [InlineData("")]
    [InlineData("acme.東京")]
    void should_reject_the_names_without_artifacts(string package)
    {
        var plan = AxonRendering.Plan(Request(new() { JavaPackage = package }));
        plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-NAMES" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error && diagnostic.Message.Contains("start with a lowercase ASCII letter", StringComparison.Ordinal)).ShouldBeTrue();
        plan.Success.ShouldBeFalse();
        plan.Artifacts.ShouldBeEmpty();
    }
}
