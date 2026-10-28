// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if DEBUG
using System.Text.RegularExpressions;

namespace Cratis.Stage.Rendering.Axon.Verification;

internal sealed record AnnotationScan(IReadOnlyDictionary<string, string[]> Annotations, IReadOnlyList<string> Findings);

internal static partial class AnnotationAllowlist
{
    [GeneratedRegex(@"\bimport\s+(?:static\s+)?(?<name>[\w.$]+(?:\.\*)?)\s*;", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex ImportPattern { get; }

    [GeneratedRegex(@"@(?<name>[\w$]+(?:\.[\w$]+)*)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex AnnotationPattern { get; }

    internal static IReadOnlySet<string> Load(string repository) => File.ReadLines(Path.Combine(repository, "Source", "Rendering.Axon.Verification", "AnnotationAllowlist.txt"))
        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#')).ToHashSet(StringComparer.Ordinal);

    internal static AnnotationScan Scan(IEnumerable<(string Path, string Source)> files)
    {
        var annotations = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var findings = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in files) ScanFile(file, annotations, findings);
        return new(annotations.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal), [.. findings]);
    }

    static void ScanFile((string Path, string Source) file, Dictionary<string, SortedSet<string>> annotations, SortedSet<string> findings)
    {
        var code = Mask(file.Source);
        var imports = Imports(code, file.Path, findings);
        foreach (Match match in AnnotationPattern.Matches(code))
        {
            var name = Resolve(match.Groups["name"].Value, imports);
            if (name.StartsWith("io.axoniq.", StringComparison.Ordinal)) findings.Add(file.Path + ": licensed annotation " + name);
            if (name.StartsWith("org.axonframework.", StringComparison.Ordinal)) Remember(annotations, name, file.Path);
        }
    }

    static void Remember(Dictionary<string, SortedSet<string>> annotations, string name, string path)
    {
        if (!annotations.TryGetValue(name, out var paths)) annotations.Add(name, paths = new(StringComparer.Ordinal));
        paths.Add(path);
    }

    static Dictionary<string, string> Imports(string code, string path, SortedSet<string> findings)
    {
        var imports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ImportPattern.Matches(code))
        {
            var name = match.Groups["name"].Value;
            if (name.StartsWith("org.axonframework.", StringComparison.Ordinal) && name.EndsWith(".*", StringComparison.Ordinal))
            {
                findings.Add(path + ": cannot verify wildcard import " + name);
            }
            if (!name.EndsWith(".*", StringComparison.Ordinal)) imports[name[(name.LastIndexOf('.') + 1)..]] = name;
        }
        return imports;
    }

    static string Resolve(string name, Dictionary<string, string> imports)
    {
        var dot = name.IndexOf('.');
        var head = dot < 0 ? name : name[..dot];
        return imports.TryGetValue(head, out var imported) ? imported + (dot < 0 ? string.Empty : name[dot..]) : name;
    }

    static string Mask(ReadOnlySpan<char> source)
    {
        var characters = source.ToArray();
        var state = "code";
        for (var index = 0; index < source.Length; index++)
        {
            var rest = source[index..];
            if (state == "code")
            {
                var opening = Opening(rest);
                if (opening.Length == 0) continue;
                state = opening;
                Blank(characters, index, opening.Length);
                index += opening.Length - 1;
                continue;
            }
            var token = Literal(rest, state);
            Blank(characters, index, token.Width);
            index += token.Width - 1;
            if (token.Closed) state = "code";
        }
        return new(characters);
    }

    static string Opening(ReadOnlySpan<char> rest)
    {
        foreach (var token in new[] { "\"\"\"", "//", "/*", "\"", "'" })
        {
            if (rest.StartsWith(token, StringComparison.Ordinal)) return token;
        }
        return string.Empty;
    }

    static (int Width, bool Closed) Literal(ReadOnlySpan<char> source, ReadOnlySpan<char> state)
    {
        var quoted = state[0] is '\"' or '\'';
        if (quoted && source[0] == '\\') return (Math.Min(2, source.Length), false);
        var closing = Closing(state);
        return source.StartsWith(closing, StringComparison.Ordinal) ? (closing.Length, true) : (1, false);
    }

    static ReadOnlySpan<char> Closing(ReadOnlySpan<char> opening) => opening switch { "//" => "\n", "/*" => "*/", _ => opening };

    static void Blank(char[] characters, int start, int length)
    {
        for (var index = start; index < start + length; index++)
        {
            if (characters[index] is not ('\r' or '\n')) characters[index] = ' ';
        }
    }
}
#endif
