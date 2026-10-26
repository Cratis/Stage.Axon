// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_appending_an_event_to_a_materialized_view : given.a_native_view_request
{
    async Task Establish()
    {
        _request = await Load("live-blueprints/models/crm");
        _context = new(_request, new(_request.Profile));
        _spec = ScopeSelection.Select(_request).SelectMany(located => located.Slice.Specifications).Single(spec => spec.WhenAppended is not null);
    }

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/DealRowsViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_enable_the_authored_specification() => _context.Specifications[_spec.Id].DisabledCode.ShouldBeNull();
    [Fact] void should_replay_the_given_event() => _java.ShouldContain("projector.on(new DealRegistered(");
    [Fact] void should_apply_the_appended_event_with_its_source() => _java.ShouldContain("projector.on(new DealMoved(new DealId(UUID.fromString(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0004\")), DealStage.fromValue(\"proposal\")), null);");
    [Fact] void should_assert_the_updated_stage() => _java.ShouldContain("Objects.equals(row.stage(), DealStage.fromValue(\"proposal\"))");
    [Fact] void should_not_disable_the_test() => _java.ShouldNotContain("@Disabled");
}
