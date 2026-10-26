// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Cratis.Screenplay.CanonicalCorpus;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications;

public class when_consuming_the_pinned_workspace_documents : Specification
{
    static readonly (string Key, string Path, string Hash)[] Expected =
    [
        ("workspaces-tracking-feature", "Workspaces/Tracking/Tracking.play", "9e8ec5f3ac6f7329ff030eccaed5e0d4ecad0cabe65137c9713821b251602f6d"),
        ("add-comment-slice", "Workspaces/Tracking/AddComment/AddComment.play", "528962089e473f36be4af7a15951b2b760517d550d9a101921ddb9e6b2ba2a3a"),
        ("close-work-item-slice", "Workspaces/Tracking/CloseWorkItem/CloseWorkItem.play", "d24144104df404432e6eb7d9a726c641c847f58a26be642065199f91921fb024"),
        ("rename-work-item-slice", "Workspaces/Tracking/RenameWorkItem/RenameWorkItem.play", "6326b960c427c4fb18b5777ee32b99498436fed2472d4ce13cb6068cb886b49a"),
        ("work-item-comments-slice", "Workspaces/Tracking/WorkItemComments/WorkItemComments.play", "857fbb63693512094246fb8b7a1d5b9a12a67827daaf3e426c22aef6920d6774"),
        ("work-item-details-slice", "Workspaces/Tracking/WorkItemDetails/WorkItemDetails.play", "ed26484b68bb7185750041912ec8475c137db599e4507f270ab1a00cf02a53fa")
    ];

    CanonicalCorpusInputs _inputs;
    CanonicalCorpusDocument[] _documents;
    string _repository;

    void Establish()
    {
        _repository = given.the_repository.Root();
        var folder = ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder");
        _documents = [.. Expected.Select(expected => folder.Documents.Single(document => document.StableKey == expected.Key))];
    }

    void Because() => _inputs = CanonicalCorpusInputs.Create(_repository);

    void Destroy() => _inputs?.Dispose();

    [Fact] void should_resolve_six_package_documents() => CanonicalCorpusInputs.PackageDocuments.Length.ShouldEqual(6);
    [Fact] void should_select_the_published_display_paths() => _documents.Select(document => document.DisplayPath).SequenceEqual(Expected.Select(expected => expected.Path)).ShouldBeTrue();
    [Fact] void should_pin_the_exact_published_bytes() => _documents.Select(document => Convert.ToHexStringLower(SHA256.HashData(document.Bytes.AsSpan()))).SequenceEqual(Expected.Select(expected => expected.Hash)).ShouldBeTrue();
    [Fact] void should_consume_the_package_bytes() => _documents.All(document => File.ReadAllBytes(Path.Combine(_inputs.Root, document.DisplayPath)).SequenceEqual(document.Bytes)).ShouldBeTrue();
    [Fact] void should_not_keep_local_copies() => _documents.Any(document => File.Exists(Path.Combine(_repository, "Corpus/live-custom-packages/model", document.DisplayPath))).ShouldBeFalse();
    [Fact] void should_compose_six_package_documents_with_the_four_retained_documents() => Directory.GetFiles(_inputs.Root, "*.play", SearchOption.AllDirectories).Length.ShouldEqual(10);

    [Fact]
    void should_remove_its_owned_input_tree_on_disposal()
    {
        var root = _inputs.Root;
        _inputs.Dispose();
        Directory.Exists(root).ShouldBeFalse();
    }
}
