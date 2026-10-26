// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandHandlerEmitter;

public class when_scalar_types_collide_with_appended_concepts : given.java_lang_collisions
{
    [Theory]
    [InlineData(SemanticPrimitiveType.Text, "String", false)]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long", false)]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean", false)]
    [InlineData(SemanticPrimitiveType.Text, "String", true)]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long", true)]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean", true)]
    public void should_qualify_only_the_colliding_scalar_return_type(SemanticPrimitiveType primitive, string name, bool collection)
    {
        Prepare(primitive, collection);
        var code = Render();
        var type = collection ? $"List<java.lang.{name}>" : "java.lang." + name;
        code.ShouldContain($"public {type} handle(");
        code.ShouldContain("return command.response();");
        code.ShouldContain($"import sample.concepts.{name};");
        code.ShouldContain($"new {name}(");
        code.ShouldNotContain($"import java.lang.{name};");
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SemanticPrimitiveType.Text, "String", false)]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long", false)]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean", false)]
    [InlineData(SemanticPrimitiveType.Text, "String", true)]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long", true)]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean", true)]
    public void should_keep_java_lang_return_types_short_without_a_collision(SemanticPrimitiveType primitive, string name, bool collection)
    {
        Prepare(primitive, collection, collide: false);
        var code = Render();
        var type = collection ? $"List<{name}>" : name;
        code.ShouldContain($"public {type} handle(");
        code.ShouldNotContain("java.lang." + name);
    }

    [Theory]
    [InlineData(SemanticComparisonOperator.GreaterThan)]
    [InlineData(SemanticComparisonOperator.GreaterThanOrEqual)]
    [InlineData(SemanticComparisonOperator.LessThan)]
    [InlineData(SemanticComparisonOperator.LessThanOrEqual)]
    public void should_resolve_java_lang_types_in_nested_append_conditions(SemanticComparisonOperator comparison)
    {
        Prepare(SemanticPrimitiveType.Text);
        command = command with { Produces = [command.Produces[0] with { When = Condition(comparison) }] };
        var code = Render();
        code.ShouldContain("(java.lang.Object)(command.count()) != null");
        code.ShouldContain("new BigDecimal(java.lang.String.valueOf(command.count()))");
        code.ShouldContain("new BigDecimal(java.lang.String.valueOf(1L))");
        code.ShouldContain("Objects.equals(command.text(), \"java.lang.String\")");
        code.ShouldContain("!(Objects.equals(command.flag(), false))");
        code.ShouldContain("import sample.concepts.String;");
        code.ShouldContain("import sample.concepts.Long;");
        code.ShouldContain("import sample.concepts.Boolean;");
        code.ShouldContain("import sample.concepts.Object;");
        code.ShouldContain("var events = new ArrayList<java.lang.Object>();");
        code.ShouldContain("public java.lang.String handle(");
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SemanticComparisonOperator.Equal)]
    [InlineData(SemanticComparisonOperator.NotEqual)]
    public void should_preserve_equality_conditions_alongside_colliding_concepts(SemanticComparisonOperator comparison)
    {
        Prepare(SemanticPrimitiveType.Boolean);
        command = command with { Produces = [command.Produces[0] with { When = Condition(comparison) }] };
        var code = Render();
        code.ShouldContain("Objects.equals(command.count(), 1L)");
        code.ShouldContain("Objects.equals(command.text(), \"java.lang.String\")");
        code.ShouldContain("!(Objects.equals(command.flag(), false))");
        code.ShouldContain("public java.lang.Boolean handle(");
        code.ShouldContain("new BigDecimal(java.lang.String.valueOf(command.count()))");
    }

    [Fact]
    public void should_keep_condition_types_short_without_a_collision()
    {
        Prepare(SemanticPrimitiveType.Text, collide: false);
        command = command with { Produces = [command.Produces[0] with { When = Condition(SemanticComparisonOperator.GreaterThan) }] };
        var code = Render();
        code.ShouldContain("(Object)(command.count()) != null");
        code.ShouldContain("new BigDecimal(String.valueOf(command.count()))");
        code.ShouldNotContain("java.lang.String.valueOf(");
        code.ShouldNotContain("java.lang.Object");
    }

    [Fact]
    public void should_resolve_a_root_condition_with_reversed_concept_and_literal_operands()
    {
        Prepare(SemanticPrimitiveType.WholeNumber);
        var condition = new SemanticComparison(
            new(default, new SemanticNumberValue(1m)),
            SemanticComparisonOperator.LessThan,
            new(command.Properties.Single(property => property.Name == "modeledCount").Id, null));
        command = command with { Produces = [command.Produces[0] with { When = condition }] };
        var code = Render();
        code.ShouldContain("(java.lang.Object)(1L) != null");
        code.ShouldContain("new BigDecimal(java.lang.String.valueOf(1L))");
        code.ShouldContain("new BigDecimal(java.lang.String.valueOf((command.modeledCount() == null ? null : command.modeledCount().value())))");
    }

    [Theory]
    [InlineData(SemanticPrimitiveType.Text, "String")]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long")]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean")]
    public void should_keep_modeled_response_types_short(SemanticPrimitiveType primitive, string name)
    {
        Prepare(primitive);
        var concept = request.Model.Application.Concepts.Single(candidate => candidate.Name == name);
        command = command with { Response = new SemanticScalarCommandResponse(response.Id, SemanticTypeReference.ForConcept(concept.Id)) };
        var code = Render();
        code.ShouldContain($"public {name} handle(");
        code.ShouldContain($"return new {name}(command.response());");
    }

    [Theory]
    [InlineData(SemanticPrimitiveType.Text, "String")]
    [InlineData(SemanticPrimitiveType.WholeNumber, "Long")]
    [InlineData(SemanticPrimitiveType.Boolean, "Boolean")]
    public void should_preserve_scalar_and_modeled_record_component_types(SemanticPrimitiveType primitive, string name)
    {
        Prepare(primitive);
        var concept = request.Model.Application.Concepts.Single(candidate => candidate.Name == name);
        command = command with
        {
            Response = new SemanticRecordCommandResponse([
                new(Name: "scalar", Type: response.Type, Source: response.Id),
                new(Name: "modeled", Type: SemanticTypeReference.ForConcept(concept.Id), Source: response.Id)])
        };
        Render().ShouldContain("public PlaceOrderResponse handle(");
        Text("PlaceOrderResponse.java").ShouldContain($"java.lang.{name} scalar, {name} modeled");
    }
}
