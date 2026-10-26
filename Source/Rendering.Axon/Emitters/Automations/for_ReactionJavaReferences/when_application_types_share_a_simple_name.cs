// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations.for_ReactionJavaReferences;

public class when_application_types_share_a_simple_name : Specification
{
    string _source;

    void Because()
    {
        var code = new JavaCodeBuilder("example.reactions").Declare("Reaction");
        const string expression = "new example.orders.OrderPlaced(), new example.shipping.OrderPlaced()";
        ReactionJavaReferences.Claim(code, [expression]);
        code.Line(ReactionJavaReferences.Expression(code, expression));
        _source = code.Build();
    }

    [Fact] void should_import_the_first_application_type() => _source.ShouldContain("import example.orders.OrderPlaced;");
    [Fact] void should_qualify_the_second_application_type() => _source.ShouldContain("new OrderPlaced(), new example.shipping.OrderPlaced()");
    [Fact] void should_not_import_the_second_type() => _source.ShouldNotContain("import example.shipping.OrderPlaced;");
}
