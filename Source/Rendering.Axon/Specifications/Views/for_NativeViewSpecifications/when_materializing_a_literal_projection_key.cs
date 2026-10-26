// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_materializing_a_literal_projection_key : given.a_native_view_request
{
    async Task Establish()
    {
        _request = await Load("live-blueprints/models/inventory");
        _context = new(_request, new(_request.Profile));
        _spec = ScopeSelection.Select(_request).SelectMany(located => located.Slice.Specifications).Single(spec => spec.Name == "CountingWhatIsInStock");
    }

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/InventoryFiguresViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_enable_the_authored_specification() => _context.Specifications[_spec.Id].DisabledCode.ShouldBeNull();
    [Fact] void should_use_the_real_projector() => _java.ShouldContain("var projector = new InventoryFiguresProjectionProjector(store);");
    [Fact] void should_assert_the_literal_projection_key() => _java.ShouldContain("store.get(\"global\").stream().anyMatch(row ->");
    [Fact] void should_assert_the_computed_quantity() => _java.ShouldContain("Objects.equals(row.unitsOnHand(), 6L)");
    [Fact] void should_assert_the_computed_count() => _java.ShouldContain("Objects.equals(row.itemCount(), 1L)");
    [Fact] void should_not_disable_the_test() => _java.ShouldNotContain("@Disabled");
}
