// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_rendering_the_verification_corpus : Specification
{
    readonly Dictionary<string, SemanticModelLoadResult> _compilations = new(StringComparer.Ordinal);
    readonly Dictionary<string, ArtifactRenderPlan> _plans = new(StringComparer.Ordinal);
    string[] blueprints;

    async Task Establish()
    {
        var root = given.blueprint_models.Root();
        blueprints = Directory.GetDirectories(Path.Combine(root, "Verification/live-blueprints/models"));
        var samples = new[] { "live-navigation", "blueprint-templates", "live-custom-packages" }
            .Select(sample => Path.Combine(root, "Verification", sample, "model"));
        foreach (var path in blueprints.Concat(samples).Order(StringComparer.Ordinal))
        {
            var name = Path.GetRelativePath(root, path);
            _compilations.Add(name, await SemanticModelLoader.LoadAsync(path, [], null, "Corpus" + _compilations.Count));
        }
    }

    void Because()
    {
        foreach (var (name, compilation) in _compilations.Where(entry => entry.Value.Success))
        {
            var loaded = compilation.Loaded;
            _plans.Add(name, AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id)));
        }
    }

    [Fact] void should_include_all_fifteen_live_blueprints() => blueprints.Length.ShouldEqual(15);
    [Fact] void should_include_the_three_verification_samples() => _compilations.Count.ShouldEqual(18);
    [Fact] void should_exclude_only_typed_upstream_compile_errors() => _compilations.Values.Where(result => !result.Success).All(result => result.Errors.Length > 0 && result.Errors.All(error => error.Code.StartsWith("PLAY", StringComparison.Ordinal))).ShouldBeTrue();
    [Fact] void should_render_every_successful_compilation() => _plans.Count.ShouldEqual(_compilations.Values.Count(result => result.Success));
    [Fact] void should_not_pass_without_rendering_any_application() => (_plans.Count > 0).ShouldBeTrue();
    [Fact] void should_keep_unsupported_constructs_nonfatal() => _plans.Values.All(plan => plan.Success && plan.Refusals.IsEmpty).ShouldBeTrue();
    [Fact] void should_emit_a_buildable_application_for_every_admitted_model() => _plans.Values.All(plan => plan.Artifacts.Any(artifact => artifact.RelativePath == "pom.xml")).ShouldBeTrue();
    [Fact] void should_emit_http_round_trips_for_every_application() => _plans.Values.All(plan => plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith("/RuntimeHttpExchangeTest.java", StringComparison.Ordinal))).ShouldBeTrue();
}
