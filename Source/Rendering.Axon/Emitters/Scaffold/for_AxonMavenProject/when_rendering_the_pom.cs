// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonMavenProject;

public class when_rendering_the_pom : Specification
{
    string _pom;

    void Because() => _pom = AxonMavenProject.Pom("sample", "app");

    [Fact] void should_pin_the_runtime_version_to_the_renderer_version() => _pom.ShouldContain($"<stage-axon-runtime.version>{AxonRendering.Version}</stage-axon-runtime.version>");
    [Fact] void should_depend_on_the_runtime_artifact_once() => Regex.Matches(_pom, "<artifactId>stage-axon-runtime</artifactId>").Count.ShouldEqual(1);
    [Fact] void should_reference_the_property_for_the_runtime_version() => _pom.ShouldContain("<dependency><groupId>io.cratis</groupId><artifactId>stage-axon-runtime</artifactId><version>${stage-axon-runtime.version}</version></dependency>");
    [Fact] void should_use_the_runtime_coordinates_of_the_seam() => AxonRuntimePackage.ArtifactId.ShouldEqual("stage-axon-runtime");
}
