// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_a_project_name : given.a_facade_model
{
    ArtifactRenderPlan _actual;
    ArtifactRenderPlan _default;

    void Establish() => _default = AxonRendering.Plan(Request(new()));
    void Because() => _actual = AxonRendering.Plan(Request(new() { ProjectName = "orders-service" }));

    [Fact]
    void should_change_only_the_artifact_id() => Text(_actual.Artifacts.Single(artifact => artifact.RelativePath == "pom.xml"))
        .ShouldContain("<groupId>facadeproof</groupId><artifactId>orders-service</artifactId>");

    [Fact]
    void should_keep_every_other_artifact() => _actual.Artifacts.Where(artifact => artifact.RelativePath != "pom.xml")
        .Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan())))
        .ShouldEqual(_default.Artifacts.Where(artifact => artifact.RelativePath != "pom.xml").Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan()))));
}
