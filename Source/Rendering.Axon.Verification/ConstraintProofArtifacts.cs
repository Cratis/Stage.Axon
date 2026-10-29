// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class ConstraintProofArtifacts
{
    internal const string Application = "constrained-java-bodies";
    const string Prefix = "ConstraintProof/";

    internal static IReadOnlyDictionary<string, string> JavaSources(string application = Application)
    {
        var package = ApplicationPackage(application);
        var assembly = typeof(ConstraintProofArtifacts).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".java", StringComparison.Ordinal))
            .ToDictionary(
                name => name[Prefix.Length..],
                name =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                    return reader.ReadToEnd().Replace("__APPLICATION__", package, StringComparison.Ordinal)
                        .Replace("__RUNTIME_MODULE__", IngressProofArtifacts.RuntimeModulePackage, StringComparison.Ordinal)
                        .Replace("__RUNTIME__", package + ".runtime", StringComparison.Ordinal);
                },
                StringComparer.Ordinal);
    }

    internal static async Task Write(string destination, string application)
    {
        var directory = Path.Combine(destination, "src/test/java", (ApplicationPackage(application) + ".runtime").Replace('.', '/'));
        Directory.CreateDirectory(directory);
        foreach (var (name, source) in JavaSources(application)) await File.WriteAllTextAsync(Path.Combine(directory, name), source);
    }

    static string ApplicationPackage(string application) => "corpus" + application.Replace("-", string.Empty, StringComparison.Ordinal);
}
