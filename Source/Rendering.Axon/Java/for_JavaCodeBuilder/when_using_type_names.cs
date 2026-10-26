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
        builder.Reference("java.util.UUID").ShouldEqual("UUID");
        builder.Reference("java.util.UUID").ShouldEqual("UUID");
        builder.Build().ShouldContain("import java.util.UUID;\n");
    }

    [Fact]
    void should_keep_the_first_framework_type_and_qualify_a_colliding_one()
    {
        var builder = new JavaCodeBuilder("example");
        builder.Reference("java.util.List").ShouldEqual("List");
        builder.Reference("java.awt.List").ShouldEqual("java.awt.List");
        var code = builder.Build();
        code.ShouldContain("import java.util.List;\n");
        code.ShouldNotContain("import java.awt.List;");
    }

    [Fact]
    void should_keep_a_declared_application_type_short_and_qualify_the_framework_type()
    {
        var builder = new JavaCodeBuilder("example").Declare("Event");
        builder.Reference("org.axonframework.eventsourcing.annotation.Event").ShouldEqual("org.axonframework.eventsourcing.annotation.Event");
        builder.Build().ShouldNotContain("import org.axonframework.eventsourcing.annotation.Event;");
    }

    [Fact]
    void should_keep_an_imported_application_type_short_and_qualify_the_framework_type()
    {
        var builder = new JavaCodeBuilder("example").Import("com.example.events.Event");
        builder.Reference("org.axonframework.eventsourcing.annotation.Event").ShouldEqual("org.axonframework.eventsourcing.annotation.Event");
        builder.Build().ShouldContain("import com.example.events.Event;\n");
    }

    [Fact]
    void should_refuse_to_claim_an_application_type_name_after_a_framework_type_took_it()
    {
        var builder = new JavaCodeBuilder("example");
        builder.Reference("org.axonframework.eventsourcing.annotation.Event");
        Catch.Exception(() => builder.Declare("Event")).ShouldBeOfExactType<SimpleNameAlreadyClaimed>();
    }

    [Fact]
    void should_not_import_java_lang_types()
    {
        var builder = new JavaCodeBuilder("example");
        builder.Reference("java.lang.Override").ShouldEqual("Override");
        builder.Build().ShouldNotContain("import java.lang.Override;");
    }
}
