// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_emitting_subscription_query_dependencies : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    XElement dependency;
    readonly XNamespace _maven = "http://maven.apache.org/POM/4.0.0";

    void Establish() => context = new(request, new AxonDegradation(request.Profile));

    void Because()
    {
        new AxonScaffoldEmitter().Emit(context);
        var pom = XDocument.Parse(Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "pom.xml").Bytes.AsSpan()));
        dependency = pom.Root.Element(_maven + "dependencies").Elements(_maven + "dependency")
            .Single(element => element.Element(_maven + "artifactId")?.Value == "reactor-core");
    }

    [Fact] void should_supply_reactor_for_axon_subscription_queries() => dependency.Element(_maven + "groupId").Value.ShouldEqual("io.projectreactor");
    [Fact] void should_use_the_spring_boot_managed_version() => dependency.Element(_maven + "version").ShouldBeNull();
    [Fact] void should_include_reactor_on_the_runtime_classpath() => dependency.Element(_maven + "scope").ShouldBeNull();
    [Fact] void should_not_make_the_runtime_dependency_optional() => dependency.Element(_maven + "optional").ShouldBeNull();
}
