// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_AxonPersistenceEmitter;

public class when_models_claim_java_lang_names : Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    readonly List<ArtifactRenderPlan> _plans = [];
    readonly List<(PlannedArtifact Artifact, string Package)> _modeled = [];
    readonly List<bool> _persistent = [];

    void Because()
    {
        var modelId = ReadModelId("Total");
        foreach (var name in new[] { "Object", "String", "Integer", "Exception", "IllegalStateException" })
        {
            SemanticFeature Feature(SemanticFeature feature) => feature with
            {
                Features = [.. feature.Features.Select(Feature)],
                Slices = [.. feature.Slices.Select(slice => slice with
                {
                    ReadModels = [.. slice.ReadModels.Select(model => model.Id == modelId ? model with { Name = name } : model)],
                    Events = [.. slice.Events.Select(contract => contract.Name == "Increased" ? contract with { Name = name } : contract)]
                })]
            };
            var application = _request.Model.Application;
            application = application with
            {
                Concepts = [application.Concepts[0] with { Name = name }, .. application.Concepts.Skip(1)],
                Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
            };
            var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application);
            var request = _request with { Model = model, ExecutionPlan = SemanticExecutionPlan.CompileForSpecifications(model).Plan! };
            var context = new AxonEmissionContext(request, new(request.Profile));
            _persistent.Add(PersistentViewAdmission.Supports(context, modelId));
            var plan = new AxonArtifactRenderPlanner().Plan(request);
            _plans.Add(plan);
            var types = new ViewJavaTypes(application, JavaNames.PackageSegment(application.Name));
            void Capture(string package, IEnumerable<string> names)
            {
                foreach (var typeName in names)
                {
                    _modeled.Add((plan.Artifacts.Single(artifact => artifact.RelativePath == $"src/main/java/{package.Replace('.', '/')}/{JavaNames.Type(typeName)}.java"), package));
                }
            }
            Capture(types.BasePackage + ".concepts", application.Concepts.Select(concept => concept.Name));
            Capture(types.BasePackage + ".types", application.Types.Select(type => type.Name));
            foreach (var owner in ScopeSelection.Select(request))
            {
                Capture(types.Package(owner), owner.Slice.ReadModels.Select(model => model.Name));
                Capture(AxonEventsEmitter.Package(context, owner.Module), owner.Slice.Events.Select(contract => contract.Name));
            }
        }
    }

    [Fact] void should_admit_persistence_for_each_hostile_name() => _persistent.TrueForAll(admitted => admitted).ShouldBeTrue();
    [Fact] void should_render_every_hostile_name() => _plans.TrueForAll(plan => plan.Success && plan.Refusals.IsEmpty).ShouldBeTrue();

    [Fact]
    void should_keep_every_modeled_type_outside_support_packages()
    {
        var packages = new ViewJavaTypes(_request.Model.Application, JavaNames.PackageSegment(_request.Model.Application.Name)).ReservedSupportPackages;
        _modeled.Count.ShouldBeGreaterThan(15);
        foreach (var (artifact, package) in _modeled)
        {
            packages.Contains(package).ShouldBeFalse();
            Text(artifact).ShouldContain("package " + package + ";");
            artifact.RelativePath.StartsWith("src/main/java/" + package.Replace('.', '/') + "/", StringComparison.Ordinal).ShouldBeTrue();
        }
    }

    [Fact]
    void should_match_every_generated_java_package_to_its_file_path()
    {
        foreach (var artifact in _plans.SelectMany(plan => plan.Artifacts).Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)))
        {
            var package = Text(artifact).Split('\n').Single(line => line.StartsWith("package ", StringComparison.Ordinal))["package ".Length..].TrimEnd(';');
            artifact.RelativePath.ShouldContain("/java/" + package.Replace('.', '/') + "/");
        }
    }

    [Fact]
    void should_preserve_bare_java_lang_names_in_support_files()
    {
        foreach (var plan in _plans)
        {
            string Code(string suffix) => Text(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith(suffix, StringComparison.Ordinal)));
            foreach (var name in new[] { "ViewRow", "ViewGeneration", "JpaReadModelStore", "PersistentViewBootstrap", "PersistentViewProcessorGuard" })
            {
                plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith("/persistence/" + name + ".java", StringComparison.Ordinal)).ShouldBeFalse();
            }
            Code("/runtime/RuntimeTest.java").ShouldContain("private final Integer value;");
        }
    }
}
