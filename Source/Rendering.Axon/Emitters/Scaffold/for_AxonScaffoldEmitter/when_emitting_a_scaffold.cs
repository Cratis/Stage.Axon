// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_emitting_a_scaffold : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(request, new AxonDegradation(request.Profile));
        new AxonScaffoldEmitter().Emit(context);
    }

    [Fact]
    void should_pin_the_verified_java_and_framework_versions()
    {
        var pom = Content("pom.xml");
        pom.ShouldContain("<java.version>25</java.version>");
        pom.ShouldContain("<version>4.1.2</version>");
        pom.ShouldContain("<version>5.4.1</version>");
        pom.ShouldContain("<arg>-Xlint:all</arg><arg>-Werror</arg>");
        pom.ShouldContain("<dependency><groupId>org.axonframework.extensions.spring</groupId><artifactId>axon-spring-boot-starter</artifactId></dependency>");
        pom.ShouldContain("<dependency><groupId>org.axonframework</groupId><artifactId>axon-test</artifactId><scope>test</scope></dependency>");
        pom.ShouldContain("<dependency><groupId>org.axonframework.extensions.spring</groupId><artifactId>axon-spring-boot-starter-test</artifactId><scope>test</scope></dependency>");
        pom.ShouldNotContain("axon-server-connector");
    }

    [Fact]
    void should_depend_on_cratis_fundamentals_0_4_0() => Content("pom.xml").ShouldContain("<dependency><groupId>io.cratis</groupId><artifactId>fundamentals</artifactId><version>0.4.0</version></dependency>");

    [Fact]
    void should_depend_on_cratis_fundamentals_jackson_0_4_0() => Content("pom.xml").ShouldContain("<dependency><groupId>io.cratis</groupId><artifactId>fundamentals-jackson</artifactId><version>0.4.0</version></dependency>");

    [Fact]
    void should_ship_both_licensed_wrapper_scripts()
    {
        Content("mvnw").ShouldContain("Apache License");
        Content("mvnw.cmd").ShouldContain("Apache License");
        Content(".mvn/wrapper/maven-wrapper.properties").ShouldContain("wrapperVersion=3.3.4");
    }

    [Fact]
    void should_disable_axon_server_and_bind_to_loopback()
    {
        var yaml = Content("src/main/resources/application.yml");
        yaml.ShouldContain("address: 127.0.0.1");
        yaml.ShouldContain("axonserver:\n    enabled: false");
    }

    [Fact]
    void should_not_configure_nonexistent_module_named_processors() => Content("src/main/resources/application.yml").ShouldNotContain("processors:");

    [Fact]
    void should_use_checkpointed_processors_for_persistent_views() => Content("src/main/resources/application-persistent.properties").ShouldContain("stage.axon.view-processor-mode=pooled");

    [Fact]
    void should_wire_standard_time_and_the_runtime_json_module()
    {
        var wiring = context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AxonConfiguration.java", StringComparison.Ordinal));
        var code = Encoding.UTF8.GetString(wiring.Bytes.AsSpan());
        code.ShouldContain("import java.time.Clock;");
        code.ShouldContain("Clock.systemUTC()");
        foreach (var name in new[] { "ConceptsModule", "ConceptKeyFormat", "TemporalModule", "TimeSpanModule" })
        {
            code.ShouldContain($"import io.cratis.fundamentals.jackson.{name};");
        }
        code.ShouldContain("JacksonModule conceptsModule() { return new ConceptsModule(ConceptKeyFormat.EMBEDDED_JSON); }");
        code.ShouldContain("JacksonModule temporalModule() { return new TemporalModule(); }");
        code.ShouldNotContain("EnumModule");
        code.ShouldContain("JacksonModule timeSpanModule() { return new TimeSpanModule(); }");
        code.ShouldNotContain(".runtime.json.");
    }

    [Fact]
    void should_replace_only_storage_beans_under_the_persistent_profile()
    {
        var wiring = context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AxonConfiguration.java", StringComparison.Ordinal));
        var code = Encoding.UTF8.GetString(wiring.Bytes.AsSpan());
        code.ShouldContain("@Profile(\"!persistent\")\n    EventStorageEngine eventStorageEngine()");
        code.ShouldContain("@Profile(\"!persistent\")\n    TokenStore tokenStore()");
        code.ShouldNotContain("uniqueValueStore(");
        code.ShouldContain("@Bean\n    Clock clock()");
        code.ShouldContain("@Bean\n    JacksonModule conceptsModule()");
    }

    string Content(string path) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
