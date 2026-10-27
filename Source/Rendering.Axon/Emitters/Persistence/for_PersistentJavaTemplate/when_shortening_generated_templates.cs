// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence.for_PersistentJavaTemplate;

public class when_shortening_generated_templates : Specification
{
    string _code;

    void Because() => _code = PersistentJavaTemplate.Render("""
        package example.proof;

        import java.util.Set;
        import org.springframework.context.annotation.Configuration;
        import static org.junit.jupiter.api.Assertions.*;

        public class Proof {
            java.util.List<java.lang.String> rows;
            java.util.Map.Entry<String, Integer> entry;
            org.axonframework.common.configuration.Configuration configuration;
            Object input;
            example.model.Object model;
            example.model.Set modeledSet;
            String literal = "java.util.List";
            @Configuration
            static class TestConfiguration {}
        }
        """);

    [Fact] void should_import_collection_types() => _code.ShouldContain("import java.util.List;");
    [Fact] void should_shorten_generic_types() => _code.ShouldContain("List<String> rows;");
    [Fact] void should_import_the_outer_type_for_nested_references() => _code.ShouldContain("import java.util.Map;");
    [Fact] void should_shorten_nested_type_references() => _code.ShouldContain("Map.Entry<String, Integer> entry;");
    [Fact] void should_preserve_static_imports() => _code.ShouldContain("import static org.junit.jupiter.api.Assertions.*;");
    [Fact] void should_preserve_string_literals() => _code.ShouldContain("String literal = \"java.util.List\";");
    [Fact] void should_preserve_the_configuration_collision_fallback() => _code.ShouldContain("org.axonframework.common.configuration.Configuration configuration;");
    [Fact] void should_not_import_the_colliding_configuration() => _code.ShouldNotContain("import org.axonframework.common.configuration.Configuration;");
    [Fact] void should_preserve_implicit_java_lang_object() => _code.ShouldContain("Object input;");
    [Fact] void should_preserve_the_modeled_object_collision_fallback() => _code.ShouldContain("example.model.Object model;");
    [Fact] void should_not_import_the_colliding_modeled_object() => _code.ShouldNotContain("import example.model.Object;");
    [Fact] void should_preserve_the_imported_set_collision_fallback() => _code.ShouldContain("example.model.Set modeledSet;");

    [Fact]
    void should_preserve_collisions_with_declared_types()
    {
        var code = PersistentJavaTemplate.Render("""
            package example.proof;

            class List {
                java.util.List<String> rows;
            }
            """);
        code.ShouldContain("java.util.List<String> rows;");
        code.ShouldNotContain("import java.util.List;");
    }
}
