// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_IngressProofArtifacts;

public partial class when_comparing_the_runtime_module_package_with_generated_output : Specification
{
    [GeneratedRegex(@"^import (?<package>[a-z][a-z0-9.]*)\.CommandValidationException;$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline, 1000)]
    private static partial Regex RuntimeImport { get; }

    CanonicalCorpusInputs _inputs;
    string _generated;

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        _inputs = CanonicalCorpusInputs.Create(repository);
        var (name, root) = CorpusApplications.Find(repository, _inputs.Root).First(application => application.Name != IngressProofModel.Application && application.Name != IngressProofModel.PersistentApplication);
        var applicationName = "Corpus" + string.Concat(name.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        var compilation = File.Exists(root)
            ? await SemanticModelLoader.LoadAsync(Path.GetDirectoryName(root)!, [Path.GetFileName(root)], null, applicationName)
            : await SemanticModelLoader.LoadAsync(root, [], null, applicationName);
        compilation.Success.ShouldBeTrue();
        var plan = AxonRendering.PlanFrom(compilation.Loaded!, new(ArtifactRenderScopeKind.Application, compilation.Loaded!.Model.Application.Id), profile: CorpusRenderer.Profile);
        plan.Success.ShouldBeTrue();
        _generated = string.Join('\n', plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)).Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan())));
    }

    void Destroy() => _inputs.Dispose();

    [Fact] void should_import_the_runtime_module_package_the_proofs_use() => RuntimeImport.Matches(_generated).Select(match => match.Groups["package"].Value).Distinct().ToArray().ShouldEqual([IngressProofArtifacts.RuntimeModulePackage]);
}
