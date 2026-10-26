// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.CanonicalCorpus;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_CorpusApplications;

public class when_the_canonical_selection_is_empty : Specification
{
    string _repository;

    void Establish() => _repository = Path.Combine(given.the_repository.Root(), ".ai-work/axon5", "missing-selection-" + Guid.NewGuid().ToString("N"));

    [Fact]
    void should_reject_missing_source_forms_before_creating_output()
    {
        Catch.Exception(() => CanonicalCorpusInputs.Create(_repository, [])).ShouldBeOfExactType<InvalidOperationException>();
        Directory.Exists(_repository).ShouldBeFalse();
    }

    [Fact]
    void should_reject_a_missing_tracking_document_before_creating_output()
    {
        Catch.Exception(() => CanonicalCorpusInputs.Create(_repository, [new() { Name = "folder" }])).ShouldBeOfExactType<InvalidOperationException>();
        Directory.Exists(_repository).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Workspaces/Tracking/Tracking.play", false)]
    [InlineData("Workspaces/Tracking/Other.play", true)]
    public void should_reject_empty_bytes_or_a_different_path_before_creating_output(string path, bool includeBytes)
    {
        var document = new CanonicalCorpusDocument { StableKey = "workspaces-tracking-feature", DisplayPath = path, Bytes = includeBytes ? [1] : [] };
        Catch.Exception(() => CanonicalCorpusInputs.Create(_repository, [new() { Name = "folder", Documents = [document] }])).ShouldBeOfExactType<InvalidOperationException>();
        Directory.Exists(_repository).ShouldBeFalse();
    }

    [Fact]
    void should_reject_duplicate_tracking_documents_before_creating_output()
    {
        var document = ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder").Documents.Single(document => document.StableKey == "workspaces-tracking-feature");
        Catch.Exception(() => CanonicalCorpusInputs.Create(_repository, [new() { Name = "folder", Documents = [document, document] }])).ShouldBeOfExactType<InvalidOperationException>();
        Directory.Exists(_repository).ShouldBeFalse();
    }

    [Fact]
    void should_reject_duplicate_source_forms_before_creating_output()
    {
        var form = ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder");
        Catch.Exception(() => CanonicalCorpusInputs.Create(_repository, [form, form])).ShouldBeOfExactType<InvalidOperationException>();
        Directory.Exists(_repository).ShouldBeFalse();
    }
}
