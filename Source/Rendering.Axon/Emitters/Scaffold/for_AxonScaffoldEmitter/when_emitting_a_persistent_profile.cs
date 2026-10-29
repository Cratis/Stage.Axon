// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_emitting_a_persistent_profile : for_PersistentProfile.given.a_constrained_orders_request
{
    AxonEmissionContext context;
    string constrainedPom;

    void Because()
    {
        context = new(request, new AxonDegradation(request.Profile));
        new AxonScaffoldEmitter().Emit(context);
        var other = new AxonEmissionContext(constrained, new(constrained.Profile));
        new AxonScaffoldEmitter().Emit(other);
        constrainedPom = Content(other, "pom.xml");
    }

    [Fact]
    void should_configure_a_file_database_and_schema_creation()
    {
        var profile = Content(context, "src/main/resources/application-persistent.yml");
        profile.ShouldContain("jdbc:h2:file:./data/");
        profile.ShouldContain("ddl-auto: update");
        profile.ShouldNotContain("profiles.active");
        profile.ShouldNotContain("active:");
    }

    [Fact] void should_keep_the_default_configuration_in_memory() => Content(context, "src/main/resources/application.yml").ShouldNotContain("datasource");

    [Fact]
    void should_emit_a_wiring_test_with_both_component_assertions()
    {
        var test = Content(context, "src/test/java/" + context.BasePackage.Replace('.', '/') + "/infrastructure/PersistentProfileTest.java");
        test.ShouldContain("assertInstanceOf(AggregateBasedJpaEventStorageEngine.class");
        test.ShouldContain("assertInstanceOf(JpaTokenStore.class");
        test.ShouldContain("@ActiveProfiles(\"persistent\")");
        test.Split('\n').Where(line => !line.StartsWith("import ", StringComparison.Ordinal) && line.Contains("org.axonframework.", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact] void should_explain_the_profile_in_the_readme() => Content(context, "README.md").ShouldContain("spring-boot.run.profiles=persistent");
    [Fact] void should_not_warn_about_the_profile() => context.Degradation.Warnings.Any(warning => warning.Code == "STAGE-AXON-PERSISTENT").ShouldBeFalse();
    [Fact] void should_not_change_the_pom() => Content(context, "pom.xml").ShouldEqual(constrainedPom);

    static string Content(AxonEmissionContext context, string path) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
