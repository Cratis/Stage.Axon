// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaCodeBuilder;

public class when_resolving_renderer_expressions : Specification
{
    string source;

    void Because()
    {
        var code = new JavaCodeBuilder("sample");
        code.ClaimExpressions(["new sample.concepts.UUID(java.util.UUID.fromString(\"java.util.UUID\"))", "sample.types.Unused.class"]);
        code.Line(code.ReferenceExpression("new sample.concepts.UUID(java.util.UUID.fromString(\"java.util.UUID\"))"));
        source = code.Build();
    }

    [Fact] void should_import_the_modeled_type() => source.ShouldContain("import sample.concepts.UUID;");
    [Fact] void should_keep_the_first_claimant_short() => source.ShouldContain("new UUID(java.util.UUID.fromString(");
    [Fact] void should_not_import_the_colliding_framework_type() => source.ShouldNotContain("import java.util.UUID;");
    [Fact] void should_preserve_qualified_text_inside_string_literals() => source.ShouldContain("\"java.util.UUID\"");
    [Fact] void should_not_import_unused_claims() => source.ShouldNotContain("import sample.types.Unused;");
}
