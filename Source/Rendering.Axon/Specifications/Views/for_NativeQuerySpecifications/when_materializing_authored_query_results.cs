// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeQuerySpecifications;

public class when_materializing_authored_query_results : for_NativeViewSpecifications.given.a_native_view_request
{
    string _details;
    string _comments;
    string _list;

    async Task Establish()
    {
        _request = await LoadCanonicalCorpus();
        _context = new(_request, new(_request.Profile));
    }

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _details = Java("ShowingRenamedDetails");
        _comments = Java("ShowingCommentsForAWorkItem");
        _list = Java("ListingCreatedWorkItems");
    }

    [Fact] void should_claim_all_three_query_specifications() => _context.Specifications.Count.ShouldEqual(3);
    [Fact] void should_enable_every_query_specification() => _context.Specifications.Values.All(spec => spec.DisabledCode is null).ShouldBeTrue();
    [Fact] void should_replay_the_created_work_item() => _details.ShouldContain("projector.on(new WorkItemCreated(");
    [Fact] void should_replay_the_rename_after_creation() => (_details.IndexOf("projector.on(new WorkItemRenamed(", StringComparison.Ordinal) > _details.IndexOf("projector.on(new WorkItemCreated(", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_query_the_real_details_handler() => _details.ShouldContain("new GetWorkItemHandler(repository).handle(new GetWorkItem(new WorkItemId(UUID.fromString(\"3fa85f64-5717-4562-b3fc-2c963f66afa6\")))).rows()");
    [Fact] void should_assert_the_renamed_title() => _details.ShouldContain("Objects.equals(row.title(), \"Implement master detail\")");
    [Fact] void should_query_the_real_comments_handler() => _comments.ShouldContain("new CommentsForWorkItemHandler(repository).handle(new CommentsForWorkItem(");
    [Fact] void should_assert_the_authored_comment() => _comments.ShouldContain("Objects.equals(row.text(), \"Needs compact layout\")");
    [Fact] void should_query_the_real_list_handler() => _list.ShouldContain("new WorkItemByIdHandler(repository).handle(new WorkItemById(");
    [Fact] void should_not_emit_disabled_placeholders() => (_details + _comments + _list).ShouldNotContain("@Disabled");

    string Java(string spec) => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + spec + "QuerySpecificationsTest.java", StringComparison.Ordinal)));
}
