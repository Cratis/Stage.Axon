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
    [Fact] void should_not_emit_a_disabled_placeholder() => _java.ShouldNotContain("@Disabled");
    [Fact] void should_use_the_real_projector() => _java.ShouldContain("var projector = new CategoriesProjectionProjector(store);");
    [Fact] void should_seed_the_authored_event_and_source_identity() => _java.ShouldContain("projector.on(new CategoryRegistered(new CategoryId(UUID.fromString(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021\")), \"Fittings\", 2L, \"Grace Hopper\", CategoryStatus.fromValue(\"visible\")), null);");
    [Fact] void should_assert_the_store_key_from_the_authored_source() => _java.ShouldContain("store.get(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021\").stream().anyMatch(row ->");
    [Fact] void should_assert_the_authored_category_identity() => _java.ShouldContain("Objects.equals(row.categoryId(), new CategoryId(UUID.fromString(\"6f1f4a3e-0c0d-4c6a-9a41-3b6a1a0e0021\")))");
    [Fact] void should_assert_the_authored_name() => _java.ShouldContain("Objects.equals(row.name(), \"Fittings\")");
    [Fact] void should_retain_the_authenticated_role() => _java.ShouldContain("SimpleGrantedAuthority(\"ROLE_CatalogManager\")");
    [Fact] void should_clear_the_caller_after_the_method() => _java.ShouldContain("finally {\n            SecurityContextHolder.clearContext();");
    [Fact] void should_import_the_test_annotation() => _java.ShouldContain("import org.junit.jupiter.api.Test;");
    [Fact] void should_import_the_display_name() => _java.ShouldContain("import org.junit.jupiter.api.DisplayName;");
    [Fact] void should_import_assertions() => _java.ShouldContain("import org.junit.jupiter.api.Assertions;");
    [Fact] void should_emit_short_assertions() => _java.ShouldContain("Assertions.assertTrue(");
    [Fact] void should_import_equality() => _java.ShouldContain("import java.util.Objects;");
    [Fact] void should_import_caller_cleanup() => _java.ShouldContain("import org.springframework.security.core.context.SecurityContextHolder;");
    [Fact] void should_not_import_unused_source_identity() => _java.ShouldNotContain("import nativeviews.runtime.EventSourceId;");
    [Fact] void should_not_invent_a_query() => _java.ShouldNotContain("new AllCategories(");
    [Fact] void should_not_claim_http_query_coverage() => _context.HttpScenarios.ContainsKey(_spec.Id).ShouldBeFalse();
}
