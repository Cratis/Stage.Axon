// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

/// <summary>
/// The corpus application whose generated TypeScript client is proven end to end: its Java tests start the app and run the
/// Node scenarios, which use only the generated client.
/// </summary>
internal static class ClientProofArtifacts
{
    internal const string Application = "client-proof";

    const string ApplicationPackage = "corpusclientproof";
    const string RuntimePackage = ApplicationPackage + ".runtime";
    const string Prefix = "ClientProof/";

    /// <summary>The Java tests, relative to the runtime test package.</summary>
    internal static IReadOnlyDictionary<string, string> JavaSources() => Resources(name => name.EndsWith(".java", StringComparison.Ordinal) && name.IndexOf('/', Prefix.Length) < 0)
        .ToDictionary(
            source => source.Key,
            source => source.Value.Replace("__APPLICATION__", ApplicationPackage, StringComparison.Ordinal).Replace("__RUNTIME__", RuntimePackage, StringComparison.Ordinal),
            StringComparer.Ordinal);

    /// <summary>The Node scenarios and their shared files, relative to <c>client/test/e2e</c>.</summary>
    internal static IReadOnlyDictionary<string, string> Scenarios() => Resources(name => name.StartsWith(Prefix + "e2e/", StringComparison.Ordinal))
        .ToDictionary(source => source.Key["e2e/".Length..], source => source.Value, StringComparer.Ordinal);

    /// <summary>The scenarios' own type-check configuration, relative to <c>client/test</c>.</summary>
    internal static string TypeCheckConfiguration() => Resources(name => name == Prefix + "tsconfig.json").Single().Value;

    internal static async Task Write(string destination)
    {
        var java = Path.Combine(destination, "src/test/java", RuntimePackage.Replace('.', '/'));
        Directory.CreateDirectory(java);
        foreach (var (name, source) in JavaSources()) await File.WriteAllTextAsync(Path.Combine(java, name), source);

        var test = Path.Combine(destination, "client/test");
        var scenarios = Path.Combine(test, "e2e");
        Directory.CreateDirectory(scenarios);
        foreach (var (name, source) in Scenarios()) await File.WriteAllTextAsync(Path.Combine(scenarios, name), source);
        await File.WriteAllTextAsync(Path.Combine(test, "tsconfig.json"), TypeCheckConfiguration());

        // Node needs to know the generated client is ESM before it strips the types from its .ts files.
        await File.WriteAllTextAsync(Path.Combine(destination, "client/package.json"), "{\n  \"private\": true,\n  \"type\": \"module\"\n}\n");
    }

    static Dictionary<string, string> Resources(Func<string, bool> selected)
    {
        var assembly = typeof(ClientProofArtifacts).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && selected(name))
            .ToDictionary(
                name => name[Prefix.Length..],
                name =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                    return reader.ReadToEnd();
                },
                StringComparer.Ordinal);
    }
}
