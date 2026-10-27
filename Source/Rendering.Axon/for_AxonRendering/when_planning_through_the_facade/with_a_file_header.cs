// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_a_file_header : given.a_facade_model
{
    ArtifactRenderPlan _actual;
    ArtifactRenderPlan _default;

    void Establish() => _default = AxonRendering.Plan(Request(new()));
    void Because() => _actual = AxonRendering.Plan(Request(new() { FileHeader = ["Copyright (c) Example"] }));

    [Fact]
    void should_prefix_every_java_file() => _actual.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
        .All(artifact => Text(artifact).StartsWith("// Copyright (c) Example\n\n", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_keep_non_java_bytes() => _actual.Artifacts.Where(artifact => !artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
        .Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan())))
        .ShouldEqual(_default.Artifacts.Where(artifact => !artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)).Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan()))));
}
