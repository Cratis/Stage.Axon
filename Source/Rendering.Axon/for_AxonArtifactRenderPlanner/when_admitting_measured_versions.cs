// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_admitting_measured_versions : Specification
{
    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(6, 6, true)]
    [InlineData(10, 10, true)]
    [InlineData(25, 25, true)]
    [InlineData(6, 2, false)]
    [InlineData(10, 6, false)]
    [InlineData(25, 10, false)]
    [InlineData(26, 26, false)]
    public void should_admit_only_exact_measured_language_and_semantic_pairs(int language, int semantic, bool supported) =>
        AxonSupportedVersions.Contains(new((uint)language, 0), new((uint)semantic, 0)).ShouldEqual(supported);

    [Fact]
    void should_reject_unmeasured_minor_versions() =>
        AxonSupportedVersions.Contains(new(25, 1), new(25, 0)).ShouldBeFalse();
}
