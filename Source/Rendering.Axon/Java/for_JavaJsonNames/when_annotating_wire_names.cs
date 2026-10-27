// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaJsonNames;

public class when_annotating_wire_names : Specification
{
    [Theory]
    [InlineData("class", "@JsonProperty(\"class\") ")]
    [InlineData("record", "@JsonProperty(\"record\") ")]
    [InlineData("customer-name", "@JsonProperty(\"customer-name\") ")]
    public void should_keep_the_modeled_name_when_the_member_name_differs(string name, string expected) =>
        JavaJsonNames.Annotation(name, new JavaCodeBuilder("example")).ShouldEqual(expected);

    [Fact] void should_emit_nothing_when_the_member_name_is_unchanged() => JavaJsonNames.Annotation("orderId", new JavaCodeBuilder("example")).ShouldEqual(string.Empty);

    [Fact]
    void should_import_the_annotation_only_when_it_is_used()
    {
        var unchanged = new JavaCodeBuilder("example");
        var renamed = new JavaCodeBuilder("example");
        JavaJsonNames.Annotation("orderId", unchanged);
        JavaJsonNames.Annotation("class", renamed);
        unchanged.Build().ShouldNotContain("JsonProperty");
        renamed.Build().ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    }
}
