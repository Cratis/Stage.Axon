// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_a_java_package : given.a_facade_model
{
    ArtifactRenderPlan _actual;
    ArtifactRenderPlan _default;

    static string Fingerprint(ArtifactRenderPlan plan) => Text(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentViewConfiguration.java", StringComparison.Ordinal)))
        .Split('\n').Single(line => line.Contains("public static final String FINGERPRINT = ", StringComparison.Ordinal)).Split('"')[1];

    void Establish() => _default = AxonRendering.Plan(Request(new()));
    void Because() => _actual = AxonRendering.Plan(Request(new() { JavaPackage = "Acme.Orders" }));

    [Fact]
    void should_move_every_java_path() => _actual.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
        .All(artifact => artifact.RelativePath.StartsWith("src/main/java/acme/orders/", StringComparison.Ordinal) || artifact.RelativePath.StartsWith("src/test/java/acme/orders/", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_set_the_maven_coordinates() => Text(_actual.Artifacts.Single(artifact => artifact.RelativePath == "pom.xml"))
        .ShouldContain("<groupId>acme.orders</groupId><artifactId>acme.orders-application</artifactId>");

    [Fact]
    void should_rename_without_introducing_qualified_types()
    {
        foreach (var artifact in _default.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)))
        {
            var renamed = _actual.Artifacts.Single(candidate => candidate.RelativePath == artifact.RelativePath.Replace("/facadeproof/", "/acme/orders/", StringComparison.Ordinal));
            Text(renamed).Replace(Fingerprint(_actual), "<fingerprint>", StringComparison.Ordinal).Split('\n').Order(StringComparer.Ordinal)
                .ShouldEqual(Text(artifact).Replace("facadeproof", "acme.orders", StringComparison.Ordinal).Replace(Fingerprint(_default), "<fingerprint>", StringComparison.Ordinal).Split('\n').Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    void should_recompute_the_persistence_fingerprint() => Assert.NotEqual(Fingerprint(_default), Fingerprint(_actual));

    [Fact]
    void should_remove_the_old_package_in_every_form() => _actual.Artifacts
        .All(artifact => !artifact.RelativePath.Contains("facadeproof", StringComparison.Ordinal) && !Text(artifact).Contains("facadeproof", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_declare_the_new_package_in_every_java_file() => _actual.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
        .All(artifact => Text(artifact).Contains("package acme.orders;", StringComparison.Ordinal) || Text(artifact).Contains("package acme.orders.", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_keep_the_renamed_bare_package_identity() => Text(_actual.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PersistentViewConfiguration.java", StringComparison.Ordinal)))
        .ShouldContain("PersistentViewBootstrap.verify(factory, \"acme.orders\", FINGERPRINT)");

    [Fact]
    void should_set_the_spring_application_name() => Text(_actual.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("application.yml", StringComparison.Ordinal))).ShouldContain("acme.orders");
}
