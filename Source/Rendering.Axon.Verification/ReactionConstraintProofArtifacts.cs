// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class ReactionConstraintProofArtifacts
{
    internal const string Application = "reaction-constraints";
    internal const string Package = "corpusreactionconstraints";

    internal static async Task Write(string destination)
    {
        foreach (var (name, source) in Sources())
        {
            var path = Path.Combine(destination, "src/test/java", Package, "runtime", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, source);
        }
    }

    internal static IReadOnlyDictionary<string, string> Sources()
    {
        var assembly = typeof(ReactionConstraintProofArtifacts).Assembly;
        var prefix = assembly.GetName().Name + ".ReactionConstraintProof.";
        return assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".java", StringComparison.Ordinal))
            .ToDictionary(name => name[prefix.Length..], name =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                return reader.ReadToEnd().Replace("__APPLICATION__", Package, StringComparison.Ordinal);
            });
    }
}
