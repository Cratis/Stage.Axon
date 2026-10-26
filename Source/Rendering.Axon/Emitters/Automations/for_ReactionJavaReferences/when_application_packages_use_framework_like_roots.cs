// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionJavaReferences;

public class when_application_packages_use_framework_like_roots : Specification
{
    [Theory]
    [InlineData("org.example.events.Clock")]
    [InlineData("io.example.events.Clock")]
    public void should_claim_the_application_before_framework_references(string applicationType)
    {
        var code = new JavaCodeBuilder("example.reactions").Declare("Reaction");
        var expression = "java.time.Clock.class, " + applicationType + ".class";
        ReactionJavaReferences.Claim(code, [expression]);
        code.Line(ReactionJavaReferences.Expression(code, expression));
        var source = code.Build();
        source.ShouldContain("import " + applicationType + ";");
        source.ShouldContain("java.time.Clock.class, Clock.class");
        source.ShouldNotContain("import java.time.Clock;");
    }
}
