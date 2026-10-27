// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class IngressProofArtifacts
{
    const string ApplicationPackage = "corpusingressinvariants";
    const string RuntimePackage = ApplicationPackage + ".runtime";

    internal static IReadOnlyDictionary<string, string> Sources()
    {
        var assembly = typeof(IngressProofArtifacts).Assembly;
        var prefix = assembly.GetName().Name + ".Templates.";
        return assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".java", StringComparison.Ordinal))
            .ToDictionary(
                name => name[prefix.Length..],
                name =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                    return reader.ReadToEnd().Replace("__APPLICATION__", ApplicationPackage, StringComparison.Ordinal).Replace("__RUNTIME__", RuntimePackage, StringComparison.Ordinal);
                },
                StringComparer.Ordinal);
    }

    internal static async Task Write(string destination)
    {
        var directory = Path.Combine(destination, "src/test/java", RuntimePackage.Replace('.', '/'));
        Directory.CreateDirectory(directory);
        foreach (var (name, source) in Sources()) await File.WriteAllTextAsync(Path.Combine(directory, name), source);
    }
}
