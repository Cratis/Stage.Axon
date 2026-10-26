// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_emitting_seeded_category_expectations : given.a_native_view_request
{
    void Because()
    {
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CategoriesViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_enable_the_authored_materialization() => _context.Specifications[_spec.Id].DisabledCode.ShouldBeNull();
    [Fact] void should_not_emit_a_disabled_placeholder() => _java.ShouldNotContain("@org.junit.jupiter.api.Disabled");
    [Fact] void should_use_the_real_projector() => _java.ShouldContain("var projector = new CategoriesProjectionProjector(store);");
    [Fact] void should_seed_the_authored_event_and_source_identity() => _java.ShouldContain("projector.on(new nativeviews.catalog.events.CategoryRegistered(new nativeviews.concepts.CategoryId(java.util.UUID.fromString(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021\")), \"Fittings\", 2L, \"Grace Hopper\", nativeviews.concepts.CategoryStatus.fromValue(\"visible\")), null);");
    [Fact] void should_assert_the_store_key_from_the_authored_source() => _java.ShouldContain("store.get(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021\").stream().anyMatch(row ->");
    [Fact] void should_assert_the_authored_category_identity() => _java.ShouldContain("java.util.Objects.equals(row.categoryId(), new nativeviews.concepts.CategoryId(java.util.UUID.fromString(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021\")))");
    [Fact] void should_assert_the_authored_name() => _java.ShouldContain("java.util.Objects.equals(row.name(), \"Fittings\")");
    [Fact] void should_retain_the_authenticated_role() => _java.ShouldContain("SimpleGrantedAuthority(\"ROLE_CatalogManager\")");
    [Fact] void should_clear_the_caller_after_the_method() => _java.ShouldContain("finally {\n            org.springframework.security.core.context.SecurityContextHolder.clearContext();");
    [Fact] void should_not_invent_a_query() => _java.ShouldNotContain("new AllCategories(");
    [Fact] void should_not_claim_http_query_coverage() => _context.HttpScenarios.ContainsKey(_spec.Id).ShouldBeFalse();
}
