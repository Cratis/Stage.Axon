// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaCodeBuilder;

public class when_using_type_names : Specification
{
    [Fact]
    void should_import_and_return_the_simple_name()
    {
        var builder = new JavaCodeBuilder("example");
        builder.Use("java.util.UUID").ShouldEqual("UUID");
        builder.Use("java.util.UUID").ShouldEqual("UUID");
        builder.Build().ShouldContain("import java.util.UUID;\n");
    }

    [Fact]
    void should_keep_the_first_type_and_qualify_a_colliding_one()
    {
        var builder = new JavaCodeBuilder("example");
        builder.Use("java.util.List").ShouldEqual("List");
        builder.Use("java.awt.List").ShouldEqual("java.awt.List");
        var code = builder.Build();
        code.ShouldContain("import java.util.List;\n");
        code.ShouldNotContain("import java.awt.List;");
    }

    [Fact]
    void should_qualify_a_type_whose_name_is_declared_locally()
    {
        var builder = new JavaCodeBuilder("example").Declare("Instant");
        builder.Use("java.time.Instant").ShouldEqual("java.time.Instant");
        builder.Build().ShouldNotContain("import java.time.Instant;");
    }

    [Fact]
    void should_not_import_java_lang_types()
    {
        var builder = new JavaCodeBuilder("example");
        builder.Use("java.lang.Override").ShouldEqual("Override");
        builder.Build().ShouldNotContain("import java.lang.Override;");
    }

    [Fact]
    void should_respect_names_claimed_by_explicit_imports()
    {
        var builder = new JavaCodeBuilder("example").Import("java.util.List");
        builder.Use("java.awt.List").ShouldEqual("java.awt.List");
        builder.Use("java.util.List").ShouldEqual("List");
    }
}
