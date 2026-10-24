// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaNames;

public class when_escaping_java_names : Specification
{
    [Theory]
    [InlineData("class", "class_")]
    [InlineData("record", "record_")]
    [InlineData("123 orders", "_123Orders")]
    [InlineData("customer-name", "customerName")]
    [InlineData("../", "generated")]
    public void should_create_safe_members(string input, string expected) => JavaNames.Member(input).ShouldEqual(expected);

    [Theory]
    [InlineData("class", "class_")]
    [InlineData("Hello / World", "helloworld")]
    [InlineData("42", "_42")]
    [InlineData("../../", "generated")]
    public void should_create_safe_package_segments(string input, string expected) => JavaNames.PackageSegment(input).ShouldEqual(expected);
}
