// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public partial class when_rendering_module_imports : Specification
{
    [GeneratedRegex(@"(?m)^import io\.cratis\.stage\.axon\.runtime\.(?<type>[\w.]+);", RegexOptions.None, 1000)]
    private static partial Regex ModuleImport { get; }

    [GeneratedRegex(@"(?m)^import (?<prefix>[\w.]+)\.runtime\.(?<type>[\w.]+);", RegexOptions.None, 1000)]
    private static partial Regex RuntimeImport { get; }

    [GeneratedRegex(@"^src/(?:main|test)/java/.+/runtime/(?<type>.+)\.java$", RegexOptions.None, 1000)]
    private static partial Regex InlinePath { get; }

    SemanticModelLoadResult _compilation;
    ArtifactRenderPlan _plan;
    Dictionary<string, string> _java;

    async Task Establish() => _compilation = await SemanticModelLoader.LoadAsync(Path.Combine(given.blueprint_models.Root(), "Corpus/axon-samples/views"), [], null, "ModuleImports");

    void Because()
    {
        var loaded = _compilation.Loaded;
        _plan = AxonRendering.PlanFrom(loaded, new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        _java = _plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
            .ToDictionary(artifact => artifact.RelativePath, artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()), StringComparer.Ordinal);
    }

    [Fact] void should_successfully_render_the_sample() => (_compilation.Success && _plan.Success && _plan.Refusals.IsEmpty && _java.Count > 0).ShouldBeTrue();
    [Fact] void should_import_only_published_module_types() => _java.Values.SelectMany(code => ModuleImport.Matches(code)).All(match => AxonRuntimePackage.ModuleTypes.Contains(match.Groups["type"].Value)).ShouldBeTrue();
    [Fact] void should_never_route_a_module_import_to_an_inline_package() => _java.Values.SelectMany(code => RuntimeImport.Matches(code)).Where(match => match.Groups["prefix"].Value != "io.cratis.stage.axon").All(match => !AxonRuntimePackage.ModuleTypes.Contains(match.Groups["type"].Value)).ShouldBeTrue();
    [Fact] void should_not_emit_module_types_inline() => _java.Keys.Select(path => InlinePath.Match(path)).Where(match => match.Success).All(match => !AxonRuntimePackage.ModuleTypes.Contains(match.Groups["type"].Value.Replace('/', '.'))).ShouldBeTrue();
    [Fact] void should_use_imports_instead_of_module_fully_qualified_names() => _java.Values.SelectMany(code => code.Split('\n')).Where(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal)).All(line => !line.Contains(AxonRuntimePackage.ModulePackage + ".", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_exercise_module_imports() => _java.Values.SelectMany(code => ModuleImport.Matches(code)).Any().ShouldBeTrue();
}
