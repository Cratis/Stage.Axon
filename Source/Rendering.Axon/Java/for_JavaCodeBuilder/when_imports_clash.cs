// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaCodeBuilder;

public class when_imports_clash
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    void should_keep_the_declared_name_and_qualify_the_import(bool declareFirst)
    {
        var builder = new JavaCodeBuilder("example");
        if (declareFirst) builder.Declare("List");
        builder.Import("java.util.List");
        if (!declareFirst) builder.Declare("List");
        builder.Reference("java.util.List").ShouldEqual("java.util.List");
        builder.Line("public class List {}");
        builder.Build().ShouldNotContain("import java.util.List;");
        builder.Build().ShouldContain("public class List {}");
    }

    [Fact]
    void should_keep_the_first_of_two_imported_names()
    {
        var builder = new JavaCodeBuilder("example").Import("alpha.Item").Import("beta.Item");
        builder.Reference("alpha.Item").ShouldEqual("Item");
        builder.Reference("beta.Item").ShouldEqual("beta.Item");
        builder.Build().ShouldContain("import alpha.Item;");
        builder.Build().ShouldNotContain("import beta.Item;");
    }
}
