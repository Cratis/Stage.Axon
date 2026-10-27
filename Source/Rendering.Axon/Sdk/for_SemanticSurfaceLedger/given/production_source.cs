// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger.given;

internal static partial class production_source
{
    [GeneratedRegex("\"(?<code>STAGE-AXON-[A-Z0-9-]+)\"", RegexOptions.NonBacktracking)]
    private static partial Regex CodeLiteral { get; }
    static readonly IReadOnlyDictionary<string, string> Files = Directory.EnumerateFiles(Path.Combine(Root(), "Source", "Rendering.Axon"), "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Contains("/for_", StringComparison.Ordinal) && !path.Contains("/given/", StringComparison.Ordinal)
            && !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal)
            && !Path.GetFileName(path).StartsWith("SemanticSurfaceLedger", StringComparison.Ordinal))
        .ToDictionary(path => path, File.ReadAllText, StringComparer.Ordinal);

    internal static IReadOnlySet<string> Codes { get; } = Files.Values
        .SelectMany(text => CodeLiteral.Matches(text).Select(match => match.Groups["code"].Value))
        .ToHashSet(StringComparer.Ordinal);

    internal static IEnumerable<string> FilesOf(Type reader)
    {
        while (reader.DeclaringType is not null) reader = reader.DeclaringType;
        var name = reader.Name.Split('`')[0];
        return Files.Where(file => Path.GetFileName(file.Key) == name + ".cs" || Path.GetFileName(file.Key).StartsWith(name + ".", StringComparison.Ordinal)).Select(file => file.Value);
    }

    static string Root([CallerFilePath] string path = "")
    {
        var directory = Directory.GetParent(path);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Stage.Axon.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find Stage.Axon.slnx.");
    }
}
