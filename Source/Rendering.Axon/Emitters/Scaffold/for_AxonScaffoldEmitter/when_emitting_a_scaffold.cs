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
        pom.ShouldContain("<java.version>21</java.version>");
        pom.ShouldContain("<version>4.1.2</version>");
        pom.ShouldContain("<version>5.4.1</version>");
        pom.ShouldContain("<arg>-Xlint:all</arg><arg>-Werror</arg>");
        pom.ShouldContain("axon-spring-boot-starter-test");
    }

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
        yaml.ShouldContain("mode: subscribing");
    }

    [Fact]
    void should_wire_standard_time_and_the_runtime_json_module()
    {
        var wiring = context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/AxonConfiguration.java", StringComparison.Ordinal));
        var code = Encoding.UTF8.GetString(wiring.Bytes.AsSpan());
        code.ShouldContain("import java.time.Clock;");
        code.ShouldContain("Clock.systemUTC()");
        code.ShouldContain(context.BasePackage + ".runtime.json.ConceptsModule");
    }

    string Content(string path) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == path).Bytes.AsSpan());
}
