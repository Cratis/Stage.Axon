// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonNamesInput;

public class when_the_java_package_is_not_canonical : for_AxonRendering.given.a_facade_model
{
    [Theory]
    [InlineData("Acme.orders")]
    [InlineData("acme.class")]
    [InlineData("acme._x")]
    [InlineData("_x.orders")]
    [InlineData("123.orders")]
    [InlineData("acme.123")]
    void should_reject_raw_profile_names_without_artifacts(string package)
    {
        var profile = AxonRendering.WithNames(AxonRendering.CreateProfile(), package, null);
        var plan = AxonRendering.PlanFrom(loaded, scope, profile: profile);
        plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-NAMES" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error).ShouldBeTrue();
        plan.Success.ShouldBeFalse();
        plan.Artifacts.ShouldBeEmpty();
    }
}
