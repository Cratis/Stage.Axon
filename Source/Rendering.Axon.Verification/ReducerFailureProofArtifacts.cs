// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class ReducerFailureProofArtifacts
{
    internal const string Application = "reducer-failure";
    internal const string PersistentApplication = "reducer-failure-persistent";

    internal static async Task Write(string destination, string application)
    {
        var package = "corpus" + application.Replace("-", string.Empty, StringComparison.Ordinal);
        var directory = Path.Combine(destination, "src/test/java", package, "runtime");
        Directory.CreateDirectory(directory);
        foreach (var (name, source) in Sources(package, application == PersistentApplication))
            await File.WriteAllTextAsync(Path.Combine(directory, name), source);
    }

    internal static IReadOnlyDictionary<string, string> Sources(string package, bool persistent)
    {
        const string file = "GeneratedReducerFailureTest.java";
        var assembly = typeof(ReducerFailureProofArtifacts).Assembly;
        using var reader = new StreamReader(assembly.GetManifestResourceStream(assembly.GetName().Name + ".Templates." + file)!);
        return new Dictionary<string, string>
        {
            [file] = reader.ReadToEnd().Replace("__APPLICATION__", package, StringComparison.Ordinal)
                .Replace("__PROFILE__", persistent ? "persistent" : "default", StringComparison.Ordinal)
        };
    }
}
