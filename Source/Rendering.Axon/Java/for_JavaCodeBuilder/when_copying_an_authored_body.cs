// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Java.for_JavaCodeBuilder;

public class when_copying_an_authored_body : Specification
{
    const string Body = "var text = \"java.time // \\\\ path\";\r\nvar block = \"\"\"\n  /* keep */\n    java.time\n  \"\"\";\nreturn;";
    string code;

    void Because() => code = new JavaCodeBuilder("example").Block("public class Example", type => type.Block("public void run()", method => method.RawBody(Body))).Build();

    [Fact] void should_preserve_every_body_byte() => Assert.Equal(Encoding.UTF8.GetBytes(Body), Encoding.UTF8.GetBytes(code.Substring(code.IndexOf(Body, StringComparison.Ordinal), Body.Length)));
    [Fact] void should_separate_the_closing_brace() => code.ShouldContain(Body + "\n    }");
    [Fact] void should_not_rewrite_qualified_names() => code.ShouldNotContain("import java.time");
}
