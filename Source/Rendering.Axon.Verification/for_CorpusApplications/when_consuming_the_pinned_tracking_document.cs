// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Cratis.Screenplay.CanonicalCorpus;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications;

public class when_consuming_the_pinned_tracking_document : Specification
{
    CanonicalCorpusInputs _inputs;
    CanonicalCorpusDocument _document;
    string _repository;

    void Establish()
    {
        _repository = given.the_repository.Root();
        _document = ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder").Documents.Single(document => document.StableKey == "workspaces-tracking-feature");
    }

    void Because() => _inputs = CanonicalCorpusInputs.Create(_repository);

    void Destroy() => _inputs?.Dispose();

    [Fact] void should_select_the_published_display_path() => _document.DisplayPath.ShouldEqual("Workspaces/Tracking/Tracking.play");
    [Fact] void should_pin_the_exact_published_bytes() => Convert.ToHexStringLower(SHA256.HashData(_document.Bytes.AsSpan())).ShouldEqual("9e8ec5f3ac6f7329ff030eccaed5e0d4ecad0cabe65137c9713821b251602f6d");
    [Fact] void should_consume_the_package_bytes() => File.ReadAllBytes(Path.Combine(_inputs.Root, _document.DisplayPath)).SequenceEqual(_document.Bytes).ShouldBeTrue();
    [Fact] void should_not_keep_a_local_tracking_copy() => File.Exists(Path.Combine(_repository, "Corpus/live-custom-packages/model", _document.DisplayPath)).ShouldBeFalse();
    [Fact] void should_compose_tracking_with_the_nine_retained_documents() => Directory.GetFiles(_inputs.Root, "*.play", SearchOption.AllDirectories).Length.ShouldEqual(10);

    [Fact]
    void should_remove_its_owned_input_tree_on_disposal()
    {
        var root = _inputs.Root;
        _inputs.Dispose();
        Directory.Exists(root).ShouldBeFalse();
    }
}
