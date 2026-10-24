// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;

namespace Cratis.Stage.Rendering.Conformance;

/// <summary>Reads native JUnit XML without treating skipped tests as passes.</summary>
public static class JUnitXmlResults
{
    /// <summary>Reads JUnit testcase results correlated by semantic display name or the nearest ancestor stage-specifications.txt manifest's class#method mapping.</summary>
    /// <param name="directory">The report directory; files are read in ordinal path order.</param>
    /// <returns>Correlated results, preserving duplicates. Non-modeled helper tests are ignored.</returns>
    /// <exception cref="FormatException">XML, a manifest or a native mapping is malformed (STAGE-CONF-008). DTDs and external entities are prohibited.</exception>
    public static ImmutableArray<SpecificationResult> ReadDirectory(string directory)
    {
        var mappings = Mappings(directory);
        return [.. Directory.EnumerateFiles(directory, "*.xml").Order(StringComparer.Ordinal).SelectMany(path => ReadFile(path, mappings))];
    }

    static Dictionary<string, string> Mappings(string directory)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        var manifest = ManifestPath(directory);
        if (manifest is null) return mappings;
        foreach (var mapping in StageSpecificationManifest.ReadMappings(manifest))
        {
            if (!mappings.TryAdd(mapping.Test, mapping.Entry.SemanticId)) throw Malformed("Duplicate native manifest mapping.");
        }
        return mappings;
    }

    static string? ManifestPath(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var manifest = Path.Combine(current.FullName, "stage-specifications.txt");
            if (File.Exists(manifest)) return manifest;
        }
        return null;
    }

    static IEnumerable<SpecificationResult> ReadFile(string path, Dictionary<string, string> mappings)
    {
        var document = Document(path);
        if (document.Root?.Name.LocalName is not ("testsuite" or "testsuites")) throw Malformed("Expected a testsuite or testsuites root.");
        foreach (var test in document.Descendants().Where(element => element.Name.LocalName == "testcase"))
        {
            var identity = Identity(test, mappings);
            if (identity is null) continue;
            var passed = !test.Elements().Any(element => element.Name.LocalName == "failure" || element.Name.LocalName == "error" || element.Name.LocalName == "skipped");
            yield return new(identity, passed);
        }
    }

    static XDocument Document(string path)
    {
        try
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new FormatException(ConformanceCodes.MalformedManifest + ": malformed or unsafe JUnit XML.", exception);
        }
    }

    static string? Identity(XElement test, Dictionary<string, string> mappings)
    {
        var name = (string?)test.Attribute("name") ?? throw Malformed("A testcase has no name.");
        var native = (string?)test.Attribute("classname") + "#" + name.TrimEnd('(', ')');
        if (mappings.TryGetValue(native, out var mapped)) return mapped;
        var display = test.Descendants().FirstOrDefault(element => element.Name.LocalName == "property" && (string?)element.Attribute("name") == "display-name")?.Attribute("value")?.Value;
        if (display?.StartsWith("sem1:", StringComparison.Ordinal) == true) return display;
        return name.StartsWith("sem1:", StringComparison.Ordinal) ? name : null;
    }

    static FormatException Malformed(string message) => new(ConformanceCodes.MalformedManifest + ": " + message);
}
