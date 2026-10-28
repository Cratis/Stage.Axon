// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class CorpusApplications
{
    // Native report verification enumerates names only; render callers supply the composed input root.
    internal static IReadOnlyList<(string Name, string Root)> Find(string repository) => Find(repository, Path.Combine(repository, "Corpus/live-custom-packages/model"));

    internal static IReadOnlyList<(string Name, string Root)> Find(string repository, string customModelRoot)
    {
        var blueprints = Directory.GetDirectories(Path.Combine(repository, "Corpus/live-blueprints/models")).Order(StringComparer.Ordinal).ToArray();
        var views = Path.Combine(repository, "Corpus/axon-samples/views");
        var files = Directory.GetFiles(views, "*.play").Order(StringComparer.Ordinal).ToArray();
        if (blueprints.Length != 15 || files.Length != 4)
        {
            throw new NativeVerificationFailed("Expected 15 live blueprints and four individual view models.");
        }

        return
        [
            .. blueprints.Select(path => (new DirectoryInfo(path).Name, path)),
            ("sample-navigation", Path.Combine(repository, "Corpus/live-navigation/model")),
            ("sample-templates", Path.Combine(repository, "Corpus/blueprint-templates/model")),
            ("sample-custom-packages", customModelRoot),
            ("views-combined", views),
            .. files.Select(path => ("view-" + Path.GetFileNameWithoutExtension(path), path)),
            (IngressProofModel.Application, Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/ingress-invariants.play")),
            (IngressProofModel.PersistentApplication, Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/ingress-invariants.play")),
            ("inline-java-policies", Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/inline-java-policies.play")),
            ("inline-java-policy-performers", Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/inline-java-policy-performers.play")),
            (ClientProofArtifacts.Application, Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/client-proof.play")),
            ("inline-java-bodies", Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/inline-java-bodies.play")),
            (ConstraintProofArtifacts.Application, Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/constrained-java-bodies.play")),
            (ConstraintProofArtifacts.PersistentApplication, Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/constrained-java-bodies.play")),
            (ReactionConstraintProofArtifacts.Application, Path.Combine(repository, "Source/Rendering.Axon.Verification/Corpus/reaction-constraints.play"))
        ];
    }
}
