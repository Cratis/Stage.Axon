// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_preserving_legacy_projection_expectations : given.a_native_view_request
{
    async Task Establish()
    {
        _request = await Load("axon-samples/views");
        _context = new(_request, new(_request.Profile));
        _located = ScopeSelection.Select(_request).Single(located => located.Slice.ReadModels.Any(model => model.Name == "Total"));
        _spec = _located.Slice.Specifications.Single(spec => spec.Name == "IncreasingTheTotal");
    }

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_keep_the_legacy_specification_enabled() => _context.Specifications[_spec.Id].DisabledCode.ShouldBeNull();
    [Fact] void should_replay_the_original_event() => _java.ShouldContain("projector.on(new Increased(new EventSourceId(\"spec-source\"), 8L), null);");
    [Fact] void should_keep_the_original_keyed_assertion() => _java.ShouldContain("repository.findById(\"global\").map(TotalEntity::toModel).stream().anyMatch(row ->");
    [Fact] void should_not_add_a_caller_to_legacy_materialization() => _java.ShouldNotContain("SecurityContextHolder");
}
