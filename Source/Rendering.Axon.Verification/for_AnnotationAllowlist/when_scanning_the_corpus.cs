// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Semantics;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_AnnotationAllowlist;

public class when_scanning_the_corpus : Specification
{
    AnnotationScan scan;
    IReadOnlySet<string> recorded;
    readonly List<string> applications = [];
    string[] discovered;

    async Task Establish()
    {
        var repository = for_CorpusApplications.given.the_repository.Root();
        recorded = AnnotationAllowlist.Load(repository);
        using var inputs = CanonicalCorpusInputs.Create(repository);
        var files = new List<(string Path, string Source)>();
        var corpus = CorpusApplications.Find(repository, inputs.Root).ToArray();
        discovered = [.. corpus.Select(application => application.Name)];
        foreach (var (name, root) in corpus)
        {
            var applicationName = "Corpus" + string.Concat(name.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            var compilation = File.Exists(root)
                ? await SemanticModelLoader.LoadAsync(Path.GetDirectoryName(root), [Path.GetFileName(root)], null, applicationName)
                : await SemanticModelLoader.LoadAsync(root, [], null, applicationName);
            Assert.True(compilation.Success);
            var plan = CorpusRenderer.Plan(name, compilation.Loaded);
            Assert.True(plan.Success);
            files.AddRange(plan.Artifacts.Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
                .Select(artifact => (name + "/" + artifact.RelativePath, Encoding.UTF8.GetString(artifact.Bytes.AsSpan()))));
            applications.Add(name);
        }
        scan = AnnotationAllowlist.Scan(files);
    }

    [Fact] void should_scan_every_application() => applications.ShouldEqual(discovered);
    [Fact] void should_scan_at_least_one_application() => applications.Count.ShouldBeGreaterThan(0);
    [Fact] void should_find_the_emitted_command_handlers() => Assert.Contains("org.axonframework.messaging.commandhandling.annotation.CommandHandler", scan.Annotations.Keys);

    [Fact]
    void should_only_emit_annotations_recorded_from_the_541_jars()
    {
        var missing = scan.Annotations.Where(entry => !recorded.Contains(entry.Key)).Select(entry => entry.Key + ": " + string.Join(", ", entry.Value));
        Assert.False(missing.Any(), string.Join('\n', missing));
    }

    [Fact] void should_have_no_wildcard_or_licensed_annotation_findings() => Assert.True(scan.Findings.Count == 0, string.Join('\n', scan.Findings));
}
