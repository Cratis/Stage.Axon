// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_lowering_nullable_equality : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string equal;
    string not_equal;
    string decimal_equal;
    string decimal_null;

    void Because()
    {
        var types = new CommandJavaTypes(loaded.Model.Application);
        var text = new SemanticTypeReference(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Text, default, false, true);
        var number = text with { Primitive = SemanticPrimitiveType.DecimalNumber };
        equal = new CommandValidationPredicates(types, new(default, SemanticValidationRuleKind.Equal, new SemanticTextValue("expected"), null), text, "command.label()").Render();
        not_equal = new CommandValidationPredicates(types, new(default, SemanticValidationRuleKind.NotEqual, new SemanticNullValue(), null), text, "command.label()").Render();
        decimal_equal = new CommandValidationPredicates(types, new(default, SemanticValidationRuleKind.Equal, new SemanticNumberValue(1.00m), null), number, "command.amount()").Render();
        decimal_null = new CommandValidationPredicates(types, new(default, SemanticValidationRuleKind.Equal, new SemanticNullValue(), null), number, "command.amount()").Render();
    }

    [Fact] void should_compare_nullable_text_without_skipping_null() => equal.ShouldEqual("java.util.Objects.equals(command.label(), \"expected\")");
    [Fact] void should_reject_null_for_not_equal_to_null() => not_equal.ShouldEqual("!(java.util.Objects.equals(command.label(), null))");
    [Fact] void should_use_scale_insensitive_decimal_equality() => decimal_equal.ShouldContain("compareTo(new java.math.BigDecimal(\"1.00\")) == 0");
    [Fact] void should_guard_decimal_comparison_against_null() => decimal_equal.ShouldContain("command.amount() != null &&");
    [Fact] void should_not_compare_decimal_numerically_to_null() => decimal_null.ShouldEqual("java.util.Objects.equals(command.amount(), null)");
}
