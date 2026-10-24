// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaCodeBuilder;

public class when_building_java : Specification
{
    [Fact]
    void should_sort_and_deduplicate_imports()
    {
        var code = new JavaCodeBuilder("example").Import("java.util.UUID").Import("java.time.Instant").Import("java.util.UUID").Build();
        code.ShouldContain("import java.time.Instant;\nimport java.util.UUID;\n\n");
        code.Split("java.util.UUID", StringSplitOptions.None).Length.ShouldEqual(2);
    }

    [Fact]
    void should_indent_nested_blocks_and_only_use_lf() =>
        new JavaCodeBuilder("example").Block("class Example", type => type.Block("void run()", method => method.Line("return;")))
            .Build().ShouldContain("class Example {\n    void run() {\n        return;\n    }\n}\n");

    [Fact]
    void should_escape_quotes_backslashes_and_control_characters() =>
        JavaCodeBuilder.StringLiteral("a\"\\\n\r\t\b\f\0").ShouldEqual("\"a\\\"\\\\\\n\\r\\t\\b\\f\\u0000\"");
}
