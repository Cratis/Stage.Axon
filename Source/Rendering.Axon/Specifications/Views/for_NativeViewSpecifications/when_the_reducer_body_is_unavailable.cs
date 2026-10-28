// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_the_reducer_body_is_unavailable : given.a_native_view_request
{
    async Task Establish()
    {
        _request = await Load("axon-samples/views");
        _request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        _context = new(_request, new(_request.Profile));
        _spec = ScopeSelection.Select(_request).SelectMany(located => located.Slice.Specifications).Single(spec => spec.Name == "RebuildFromEvents");
    }

    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalQuarantineSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_enable_the_refusal_specification() => _context.Specifications[_spec.Id].DisabledCode.ShouldBeNull();
    [Fact] void should_explain_the_unavailable_reduction() => _java.ShouldContain("quarantined; modeled reduction is unavailable");
    [Fact] void should_assert_store_refusal() => _java.ShouldContain("Assertions.assertThrows(IllegalStateException.class, () -> store.all());");
    [Fact] void should_not_import_the_disabled_annotation() => _java.ShouldNotContain("import org.junit.jupiter.api.Disabled;");
    [Fact] void should_import_the_test_annotation() => _java.ShouldContain("import org.junit.jupiter.api.Test;");
    [Fact] void should_emit_a_short_test_annotation() => _java.ShouldContain("@Test");
    [Fact] void should_not_claim_native_reducer_execution() => _java.ShouldNotContain("var projector =");
}
