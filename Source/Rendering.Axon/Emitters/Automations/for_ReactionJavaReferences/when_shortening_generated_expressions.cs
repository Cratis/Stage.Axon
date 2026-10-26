// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionJavaReferences;

public class when_shortening_generated_expressions : Specification
{
    const string Expression = "org.axonframework.messaging.commandhandling.gateway.CommandGateway.class, new example.commands.CommandGateway(java.util.List.of(\"java.time.Instant\", example.events.EventTags.ORDER_ID))";
    string _source;

    void Because()
    {
        var code = new JavaCodeBuilder("example.reactions").Declare("Reaction");
        ReactionJavaReferences.Claim(code, [Expression]);
        code.Line(ReactionJavaReferences.Expression(code, Expression));
        _source = code.Build();
    }

    [Fact] void should_import_the_application_types() => _source.ShouldContain("import example.commands.CommandGateway;");
    [Fact] void should_claim_application_types_before_framework_types() => _source.ShouldContain("org.axonframework.messaging.commandhandling.gateway.CommandGateway.class, new CommandGateway(");
    [Fact] void should_not_import_the_conflicting_framework_type() => _source.ShouldNotContain("import org.axonframework.messaging.commandhandling.gateway.CommandGateway;");
    [Fact] void should_preserve_literals_and_static_members() => _source.ShouldContain("List.of(\"java.time.Instant\", EventTags.ORDER_ID)");
    [Fact] void should_import_non_conflicting_framework_types() => _source.ShouldContain("import java.util.List;");
    [Fact] void should_not_treat_nested_simple_names_as_packages() => ReactionJavaReferences.Expression(new("example.reactions"), "Flow.Publisher.class, Long.MAX_VALUE").ShouldEqual("Flow.Publisher.class, Long.MAX_VALUE");
}
