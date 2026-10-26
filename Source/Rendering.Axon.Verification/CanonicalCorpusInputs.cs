// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.CanonicalCorpus;

namespace Cratis.Stage.Rendering.Axon.Verification;

internal sealed class CanonicalCorpusInputs : IDisposable
{
    internal static readonly (string StableKey, string DisplayPath)[] PackageDocuments =
    [
        ("workspaces-tracking-feature", "Workspaces/Tracking/Tracking.play"),
        ("add-comment-slice", "Workspaces/Tracking/AddComment/AddComment.play"),
        ("close-work-item-slice", "Workspaces/Tracking/CloseWorkItem/CloseWorkItem.play"),
        ("rename-work-item-slice", "Workspaces/Tracking/RenameWorkItem/RenameWorkItem.play"),
        ("work-item-comments-slice", "Workspaces/Tracking/WorkItemComments/WorkItemComments.play"),
        ("work-item-details-slice", "Workspaces/Tracking/WorkItemDetails/WorkItemDetails.play")
    ];

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
        var folder = sourceForms.Single(form => form.Name == "folder");
        var documents = PackageDocuments.Select(package => Resolve(folder, package.StableKey, package.DisplayPath)).ToArray();
        if (documents.Length == 0)
        {
            throw new InvalidOperationException("Expected at least one canonical package document.");
        }

        var inputs = new CanonicalCorpusInputs(Path.Combine(repository, ".ai-work/axon5", "inputs-" + Guid.NewGuid().ToString("N")));
        try
        {
            CopyRetainedInputs(Path.Combine(repository, "Corpus/live-custom-packages/model"), inputs.Root);
            foreach (var document in documents)
            {
                WritePackageDocument(inputs.Root, document);
            }

            return inputs;
        }
        catch
        {
            inputs.Dispose();
            throw;
        }
    }

    static CanonicalCorpusDocument Resolve(CanonicalCorpusSourceForm folder, string stableKey, string displayPath)
    {
        var document = folder.Documents.Single(document => document.StableKey == stableKey);
        if (document.DisplayPath != displayPath || document.Bytes.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException($"Expected the nonempty canonical {displayPath} document.");
        }

        return document;
    }

    static void WritePackageDocument(string root, CanonicalCorpusDocument document)
    {
        var target = Path.Combine(root, document.DisplayPath);
        if (File.Exists(target))
        {
            throw new InvalidOperationException($"{document.DisplayPath} is still vendored; the package document must not shadow a local copy.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, document.Bytes.AsSpan());
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
