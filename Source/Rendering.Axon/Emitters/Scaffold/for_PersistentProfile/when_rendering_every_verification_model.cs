// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Verification;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold.for_PersistentProfile;

/// <summary>Tripwire: a model that gets the persistent profile must never emit multi-tag events or multi-entity appends, because the JPA engine cannot hold them.</summary>
public class when_rendering_every_verification_model : Specification
{
    readonly List<(string Name, ArtifactRenderPlan Plan)> _persistent = [];
    CanonicalCorpusInputs _inputs;

    async Task Establish()
    {
        var root = for_AxonArtifactRenderPlanner.given.blueprint_models.Root();
        _inputs = CanonicalCorpusInputs.Create(root);
        var samples = new[] { "live-navigation", "blueprint-templates", "live-custom-packages" }
            .Select(sample => Path.Combine(root, "Corpus", sample, "model"));
        var views = Path.Combine(root, "Corpus/axon-samples/views");
        var paths = Directory.GetDirectories(Path.Combine(root, "Corpus/live-blueprints/models")).Concat(samples).Append(views).Concat(Directory.GetFiles(views, "*.play")).Order(StringComparer.Ordinal);
        var index = 0;
        foreach (var path in paths)
        {
            var input = path == Path.Combine(root, "Corpus/live-custom-packages/model") ? _inputs.Root : path;
            var compilation = File.Exists(input)
                ? await SemanticModelLoader.LoadAsync(Path.GetDirectoryName(input), [Path.GetFileName(input)], null, "Persistent" + index++)
                : await SemanticModelLoader.LoadAsync(input, [], null, "Persistent" + index++);
            if (!compilation.Success) continue;
            var loaded = compilation.Loaded;
            var plan = AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
            var name = Path.GetRelativePath(root, path);
            if (HasProfile(plan)) _persistent.Add((name, plan));
        }
    }

    void Destroy() => _inputs?.Dispose();

    [Fact] void should_offer_the_profile_to_some_model() => (_persistent.Count > 0).ShouldBeTrue();
    [Fact] void should_not_emit_multi_tag_events_where_the_profile_is_offered() => Java(_persistent).Where(source => Count(source.Text, "@EventTag") > 1).Select(source => source.Path).ShouldBeEmpty();
    [Fact] void should_not_inject_several_entities_into_one_handler_where_the_profile_is_offered() => Java(_persistent).Where(source => source.Text.Split('\n').Any(line => Count(line, "@InjectEntity") > 1)).Select(source => source.Path).ShouldBeEmpty();
    [Fact] void should_generate_the_profile_only_with_its_test_and_wiring() => _persistent.TrueForAll(item => Has(item.Plan, "/PersistentProfileTest.java") && Has(item.Plan, "src/main/resources/application-persistent.yml")).ShouldBeTrue();

    static int Count(string text, string token) => text.Split(token).Length - 1;

    static bool HasProfile(ArtifactRenderPlan plan) => Has(plan, "src/main/resources/application-persistent.yml");

    static bool Has(ArtifactRenderPlan plan, string path) => plan.Artifacts.Any(artifact => artifact.RelativePath.EndsWith(path, StringComparison.Ordinal));

    static IEnumerable<(string Path, string Text)> Java(IEnumerable<(string Name, ArtifactRenderPlan Plan)> items) => items
        .SelectMany(item => item.Plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)).Select(artifact => (item.Name + "/" + artifact.RelativePath, Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))));
}
