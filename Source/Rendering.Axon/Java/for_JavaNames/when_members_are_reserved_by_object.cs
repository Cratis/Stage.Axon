// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaNames;

public class when_members_are_reserved_by_object
{
    [Theory]
    [InlineData("getClass")]
    [InlineData("hashCode")]
    [InlineData("equals")]
    [InlineData("toString")]
    [InlineData("clone")]
    [InlineData("finalize")]
    [InlineData("notify")]
    [InlineData("notifyAll")]
    [InlineData("wait")]
    void should_rename_the_java_member_and_preserve_the_json_wire_name(string name)
    {
        JavaNames.Member(name).ShouldEqual(name + "_");
        var builder = new JavaCodeBuilder("example");
        JavaJsonNames.Annotation(name, builder).ShouldEqual($"@JsonProperty(\"{name}\") ");
        builder.Build().ShouldContain("import com.fasterxml.jackson.annotation.JsonProperty;");
    }

    [Fact]
    void should_leave_non_reserved_members_unchanged() => JavaNames.Member("value").ShouldEqual("value");
}
