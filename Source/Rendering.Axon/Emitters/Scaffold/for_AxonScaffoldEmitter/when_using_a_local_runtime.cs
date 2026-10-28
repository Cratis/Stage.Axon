// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_AxonScaffoldEmitter;

public class when_using_a_local_runtime : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string pom;

    void Because()
    {
        context = new(request, new AxonDegradation(AxonRendering.CreateProfile(strict: true)));
        AxonScaffoldEmitter.Emit(context, "0.0.0-local");
        pom = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "pom.xml").Bytes.AsSpan());
    }

    [Fact]
    void should_warn_even_in_strict_mode()
    {
        var warning = context.Degradation.Warnings.Single();
        warning.Code.ShouldEqual("STAGE-AXON-LOCAL-RUNTIME");
        warning.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        warning.Message.ShouldEqual(AxonMavenProject.LocalBuildWarning("0.0.0-local"));
        warning.Message.ShouldContain("io.cratis:stage-axon-runtime 0.0.0-local, which is not published");
        warning.Message.ShouldContain("sh ./mvnw -Drevision=0.0.0-local install");
        warning.Message.ShouldContain("Stage.Axon's Source/Runtime, or use a released renderer");
        context.Degradation.Refusals.ShouldBeEmpty();
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Fact]
    void should_explain_the_local_pin_in_an_adjacent_xml_comment()
    {
        XNamespace ns = "http://maven.apache.org/POM/4.0.0";
        var pin = XDocument.Parse(pom).Root.Element(ns + "properties").Element(ns + "stage-axon-runtime.version");
        pin.Value.ShouldEqual("0.0.0-local");
        ((XComment)pin.PreviousNode).Value.Trim().ShouldEqual(context.Degradation.Warnings.Single().Message);
    }

    [Fact]
    void should_use_the_assembly_version_by_default()
    {
        var actual = new AxonEmissionContext(request, new AxonDegradation(request.Profile));
        new AxonScaffoldEmitter().Emit(actual);
        actual.Degradation.Warnings.Any(warning => warning.Code == "STAGE-AXON-LOCAL-RUNTIME").ShouldEqual(AxonMavenProject.IsLocalVersion(AxonRendering.Version));
        AxonMavenProject.Pom(context.BasePackage, context.ArtifactId).ShouldContain($"<stage-axon-runtime.version>{AxonRendering.Version}</stage-axon-runtime.version>");
    }
}
