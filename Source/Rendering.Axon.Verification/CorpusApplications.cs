// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class CorpusApplications
{
    internal static IReadOnlyList<(string Name, string Root)> Find(string repository)
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
            ("sample-custom-packages", Path.Combine(repository, "Corpus/live-custom-packages/model")),
            ("views-combined", views),
            .. files.Select(path => ("view-" + Path.GetFileNameWithoutExtension(path), path))
        ];
    }
}
