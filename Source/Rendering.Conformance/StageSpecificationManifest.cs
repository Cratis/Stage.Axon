// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Reads the renderer's tab-separated specification manifest.</summary>
public static class StageSpecificationManifest
{
    /// <summary>Reads SemanticId, optional "disabled CODE", and native class#method columns.</summary>
    /// <param name="path">The stage-specifications.txt path.</param>
    /// <returns>Entries in file order, preserving duplicates for coverage diagnostics.</returns>
    /// <exception cref="FormatException">An entry is malformed (STAGE-CONF-008).</exception>
    public static ImmutableArray<SpecificationManifestEntry> Read(string path) => [.. ReadMappings(path).Select(mapping => mapping.Entry)];

    internal static IEnumerable<(SpecificationManifestEntry Entry, string Test)> ReadMappings(string path) =>
        File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Select(Parse);

    static (SpecificationManifestEntry Entry, string Test) Parse(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length is not (2 or 3)) throw Malformed();
        if (fields.Any(string.IsNullOrWhiteSpace) || fields.Any(field => field != field.Trim())) throw Malformed();
        var native = fields[^1];
        var separator = native.IndexOf('#', StringComparison.Ordinal);
        if (separator <= 0 || separator == native.Length - 1) throw Malformed();
        if (native.LastIndexOf('#') != separator) throw Malformed();
        return (new(fields[0], DisabledCode(fields)), native);
    }

    static string? DisabledCode(string[] fields)
    {
        if (fields.Length == 2) return null;
        const string prefix = "disabled ";
        if (!fields[1].StartsWith(prefix, StringComparison.Ordinal)) throw Malformed();
        var code = fields[1][prefix.Length..];
        if (string.IsNullOrWhiteSpace(code) || code.Any(char.IsWhiteSpace)) throw Malformed();
        return code;
    }

    static FormatException Malformed() => new(ConformanceCodes.MalformedManifest + ": malformed stage-specifications entry.");
}
