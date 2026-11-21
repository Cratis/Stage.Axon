// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations.given;

internal static partial class executable_entries
{
    internal sealed record Entry(string Path, IReadOnlyCollection<SemanticId> Sources, string Declaration);

    internal static Entry[] From(ArtifactRenderPlan plan) => [.. plan.Artifacts
        .Where(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal))
        .SelectMany(artifact => Declarations(artifact.RelativePath, artifact.Bytes.AsSpan())
            .Select(declaration => new Entry(artifact.RelativePath, [.. artifact.Sources], declaration)))];

    static string[] Declarations(string path, ReadOnlySpan<byte> bytes)
    {
        var source = Encoding.UTF8.GetString(bytes);
        var declarations = AnnotatedDeclarations.Matches(source).Select(match => match.Value).Where(declaration => Dispatch.IsMatch(declaration)).ToArray();

        // Do not silently lose an entry point if an emitter changes its annotation/declaration layout.
        Assert.True(declarations.Sum(Dispatch.Count) == Dispatch.Count(source), path);
        return declarations;
    }

    // EventSourcingHandler replays state inside an entity; it is not executable ingress. Include lifecycle and scheduled dispatch as well as HTTP and Axon handlers.
    [GeneratedRegex(@"@(?:[\w]+\.)*(?:CommandHandler|QueryHandler|EventHandler|PostMapping|GetMapping|RequestMapping|PutMapping|PatchMapping|DeleteMapping|EventListener|Scheduled)\b", RegexOptions.None, 1000)]
    private static partial Regex Dispatch { get; }

    [GeneratedRegex(@"(?:^[ \t]*@[^\r\n]+\r?\n)+[ \t]*[^@\s][^\r\n]+", RegexOptions.Multiline, 1000)]
    private static partial Regex AnnotatedDeclarations { get; }
}
