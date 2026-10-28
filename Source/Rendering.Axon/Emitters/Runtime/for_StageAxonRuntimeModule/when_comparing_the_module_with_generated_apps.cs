// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;
using Cratis.Stage.Rendering.Axon.Emitters.Scaffold;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_StageAxonRuntimeModule;

public class when_comparing_the_module_with_generated_apps : Specification
{
    string _module;
    string _app;

    void Because()
    {
        _module = File.ReadAllText(Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Source", "Runtime", "pom.xml"));
        _app = AxonMavenProject.Pom("sample", "app");
    }

    [Fact] void should_use_the_spring_boot_version_of_the_generated_parent() => Version(_module, "spring-boot-dependencies").ShouldEqual(Version(_app, "spring-boot-starter-parent"));
    [Fact] void should_use_the_axon_bom_of_the_generated_apps() => Version(_module, "axon-framework-bom").ShouldEqual(Version(_app, "axon-framework-bom"));
    [Fact] void should_use_the_fundamentals_version_of_the_generated_apps() => Version(_module, CratisFundamentals.ArtifactId).ShouldEqual(CratisFundamentals.Version);
    [Fact] void should_use_the_runtime_group() => _module.ShouldContain($"<groupId>{AxonRuntimePackage.GroupId}</groupId>\n  <artifactId>{AxonRuntimePackage.ArtifactId}</artifactId>");
    [Fact] void should_publish_the_package_the_verification_proofs_import() => AxonRuntimePackage.ModulePackage.ShouldEqual("io.cratis.stage.axon.runtime");
    [Fact] void should_compile_for_the_generated_java_release() => Property(_module, "maven.compiler.release").ShouldEqual(Property(_app, "java.version"));

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static string Version(string pom, string artifact) => Regex.Match(pom, $"<artifactId>{Regex.Escape(artifact)}</artifactId>\\s*<version>([^<]+)</version>", RegexOptions.None, Timeout).Groups[1].Value;

    static string Property(string pom, string name) => Regex.Match(pom, $"<{Regex.Escape(name)}>([^<]+)</{Regex.Escape(name)}>", RegexOptions.None, Timeout).Groups[1].Value;
}
