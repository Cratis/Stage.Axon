// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Cratis.Stage.Rendering.Axon.Verification.for_AnnotationAllowlist;

public class when_resolving_annotations
{
    [Fact]
    public void should_resolve_imported_qualified_and_nested_annotations()
    {
        var scan = AnnotationAllowlist.Scan([("Example.java", """
            import org.axonframework.messaging.commandhandling.annotation.CommandHandler;
            import org.axonframework.example.Outer;
            @CommandHandler class Example {}
            @org.axonframework.example.Qualified class Qualified {}
            @Outer.Inner class Nested {}
            """)]);
        Assert.Equal(["Example.java"], scan.Annotations["org.axonframework.messaging.commandhandling.annotation.CommandHandler"]);
        Assert.Contains("org.axonframework.example.Qualified", scan.Annotations.Keys);
        Assert.Contains("org.axonframework.example.Outer.Inner", scan.Annotations.Keys);
        Assert.Empty(scan.Findings);
    }

    [Fact]
    public void should_ignore_comments_literals_and_unresolved_java_annotations()
    {
        const string source = "import org.axonframework.example.Foo;\n" +
            "// @Foo\n/* @Foo */\n/** @param value */\n@Override class Example {\n" +
            "String a = \"@Foo \\\" @Foo\"; char b = '\\'';\n" +
            "String block = \"\"\"\n@Foo\n\"\"\"; }";
        var scan = AnnotationAllowlist.Scan([("Ignored.java", source)]);
        Assert.Empty(scan.Annotations);
        Assert.Empty(scan.Findings);
    }

    [Fact]
    public void should_report_unverifiable_wildcards_and_licensed_annotations()
    {
        var scan = AnnotationAllowlist.Scan([("Banned.java", """
            import org.axonframework.example.*;
            import io.axoniq.example.Licensed;
            @Licensed class Example {}
            @io.axoniq.example.Other class Other {}
            """)]);
        Assert.Equal(3, scan.Findings.Count);
        Assert.All(scan.Findings, finding => Assert.Contains("Banned.java", finding, StringComparison.Ordinal));
    }
}
