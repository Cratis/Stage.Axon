// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class IngressProofArtifacts
{
    /// <summary>The Java package of the published runtime module; a Rendering.Axon spec asserts the renderer emits the same package.</summary>
    internal const string RuntimeModulePackage = "io.cratis.stage.axon.runtime";

    const string ApplicationPackage = "corpusingressinvariants";
    const string RuntimePackage = ApplicationPackage + ".runtime";

    internal static IReadOnlyDictionary<string, string> Sources()
    {
        var assembly = typeof(IngressProofArtifacts).Assembly;
        var prefix = assembly.GetName().Name + ".Templates.";

        // The reducer quarantine proof has its own application/package substitutions and writer.
        return assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                && name.EndsWith(".java", StringComparison.Ordinal) && name != prefix + "GeneratedReducerFailureTest.java")
            .ToDictionary(
                name => name[prefix.Length..],
                name =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                    return reader.ReadToEnd().Replace("__APPLICATION__", ApplicationPackage, StringComparison.Ordinal).Replace("__RUNTIME_MODULE__", IngressProofArtifacts.RuntimeModulePackage, StringComparison.Ordinal).Replace("__RUNTIME__", RuntimePackage, StringComparison.Ordinal);
                },
                StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, string> Sources(string application)
    {
        string[] selected = application switch
        {
            "inline-java-policies" => ["GeneratedInlinePolicyTest.java", "GeneratedInlinePolicyDemoTest.java"],
            "inline-java-policy-performers" => ["GeneratedInlinePolicyPerformerTest.java"],
            IngressProofModel.Application => ["GeneratedIngressSupport.java", "GeneratedProtectedReadTest.java", "GeneratedMemoryProtectedReadTest.java", "GeneratedSecurityIngressTest.java", "GeneratedDemoIdentityTest.java"],
            IngressProofModel.PersistentApplication => ["GeneratedIngressSupport.java", "GeneratedProtectedReadTest.java", "GeneratedPersistentProtectedReadTest.java", "GeneratedPersistentUniqueConcurrencyTest.java"],
            _ => throw new NativeVerificationFailed("Unknown ingress proof application: " + application)
        };
        return Sources().Where(source => selected.Contains(source.Key, StringComparer.Ordinal))
            .ToDictionary(source => source.Key, source => source.Value.Replace(ApplicationPackage, Package(application), StringComparison.Ordinal), StringComparer.Ordinal);
    }

    internal static async Task Write(string destination, string application)
    {
        var directory = Path.Combine(destination, "src/test/java", (Package(application) + ".runtime").Replace('.', '/'));
        Directory.CreateDirectory(directory);
        foreach (var (name, source) in Sources(application)) await File.WriteAllTextAsync(Path.Combine(directory, name), source);
    }

    static string Package(string application) => application switch
    {
        "inline-java-policies" => "corpusinlinejavapolicies",
        "inline-java-policy-performers" => "corpusinlinejavapolicyperformers",
        IngressProofModel.Application => ApplicationPackage,
        IngressProofModel.PersistentApplication => "corpusingresspersistent",
        _ => throw new NativeVerificationFailed("Unknown ingress proof application: " + application)
    };
}
