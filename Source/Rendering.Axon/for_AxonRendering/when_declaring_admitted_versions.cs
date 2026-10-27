// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering;

public class when_declaring_admitted_versions : Specification
{
    SemanticVersion _newest;

    void Because() => _newest = AxonRendering.NewestAdmittedSemanticVersion;

    [Fact] void should_declare_v34() => _newest.ShouldEqual(SemanticVersion.V34);

    [Fact]
    void should_require_review_on_a_screenplay_version_bump() => _newest.ShouldEqual(typeof(SemanticVersion)
        .GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(SemanticVersion))
        .Select(field => (SemanticVersion)field.GetValue(null)!).OrderBy(version => version.Major).ThenBy(version => version.Minor).Last());

    [Theory]
    [InlineData(2u)]
    [InlineData(6u)]
    [InlineData(10u)]
    [InlineData(25u)]
    void should_declare_the_measured_pairs(uint version) => AxonRendering.IsMeasured(new(version, 0), new(version, 0)).ShouldBeTrue();

    [Fact] void should_not_declare_an_unmeasured_pair() => AxonRendering.IsMeasured(new(32, 0), new(32, 0)).ShouldBeFalse();
}
