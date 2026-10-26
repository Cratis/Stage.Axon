// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.CanonicalCorpus;

namespace Cratis.Stage.Rendering.Axon.Verification;

internal sealed class CanonicalCorpusInputs : IDisposable
{
    const string TrackingPath = "Workspaces/Tracking/Tracking.play";

    CanonicalCorpusInputs(string root) => Root = root;

    internal string Root { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    internal static CanonicalCorpusInputs Create(string repository) => Create(repository, ScreenCompositionCorpus.V1.SourceForms);

    internal static CanonicalCorpusInputs Create(string repository, IEnumerable<CanonicalCorpusSourceForm> sourceForms)
    {
        var document = sourceForms.Single(form => form.Name == "folder").Documents.Single(document => document.StableKey == "workspaces-tracking-feature");
        if (document.DisplayPath != TrackingPath || document.Bytes.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException("Expected the nonempty canonical Workspaces/Tracking/Tracking.play document.");
        }

        var inputs = new CanonicalCorpusInputs(Path.Combine(repository, ".ai-work/axon5", "inputs-" + Guid.NewGuid().ToString("N")));
        try
        {
            CopyRetainedInputs(Path.Combine(repository, "Corpus/live-custom-packages/model"), inputs.Root);
            var tracking = Path.Combine(inputs.Root, TrackingPath);
            Directory.CreateDirectory(Path.GetDirectoryName(tracking)!);
            File.WriteAllBytes(tracking, document.Bytes.AsSpan());
            return inputs;
        }
        catch
        {
            inputs.Dispose();
            throw;
        }
    }

    static void CopyRetainedInputs(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
