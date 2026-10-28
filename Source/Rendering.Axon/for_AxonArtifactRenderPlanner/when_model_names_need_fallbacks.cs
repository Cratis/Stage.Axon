// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_model_names_need_fallbacks : a_view_request
{
    [Theory]
    [InlineData("Java", "java")]
    [InlineData("Javax", "javax")]
    [InlineData("Jdk", "jdk")]
    [InlineData("Sun", "sun")]
    void should_prefix_a_jdk_root_in_the_single_package_source(string name, string normalized)
    {
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, _request.Model.Application with { Name = name });
        var request = _request with { Model = model, ExecutionPlan = SemanticExecutionPlan.CompileForSpecifications(model).Plan! };
        AxonNamesInput.BasePackage(request).ShouldEqual("generated." + normalized);
        var plan = new AxonArtifactRenderPlanner().Plan(request);
        plan.Success.ShouldBeTrue();
        plan.Diagnostics.Single(diagnostic => diagnostic.Code == "STAGE-AXON-NAMES").Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        plan.Artifacts.Any(artifact => artifact.RelativePath.StartsWith("src/main/java/" + normalized + "/", StringComparison.Ordinal)).ShouldBeFalse();
        plan.Artifacts.Any(artifact => artifact.RelativePath.StartsWith("src/main/java/generated/" + normalized + "/", StringComparison.Ordinal)).ShouldBeTrue();
        Text(plan.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md")).ShouldContain("JDK-root base package");
    }

    [Fact]
    void should_warn_when_a_record_member_is_renamed_and_keep_its_wire_name()
    {
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                ReadModels = [.. slice.ReadModels.Select(model => model.Name != "Total" ? model : model with
                {
                    Properties = [.. model.Properties.Select((property, index) => index == 0 ? property with { Name = "hashCode" } : property)]
                })]
            })]
        };
        var application = _request.Model.Application;
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        });
        var plan = new AxonArtifactRenderPlanner().Plan(_request with { Model = model, ExecutionPlan = SemanticExecutionPlan.CompileForSpecifications(model).Plan! });
        var diagnostic = plan.Diagnostics.Single(diagnostic => diagnostic.Code == "STAGE-AXON-NAMES");
        diagnostic.Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("hashCode_");
        var java = Text(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Total.java", StringComparison.Ordinal)));
        java.ShouldContain("@JsonProperty(\"hashCode\")");
        java.ShouldContain("hashCode_");
    }

    [Fact]
    void should_explain_that_inline_packages_are_not_rewritten()
    {
        _request.ImplementationContents.Count.ShouldBeGreaterThan(0);
        var before = _request.ImplementationContents.ToArray();
        var plan = new AxonArtifactRenderPlanner().Plan(_request);
        Text(plan.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md")).ShouldContain("Hard-coded package names");
        _request.ImplementationContents.SequenceEqual(before).ShouldBeTrue();
    }
}
