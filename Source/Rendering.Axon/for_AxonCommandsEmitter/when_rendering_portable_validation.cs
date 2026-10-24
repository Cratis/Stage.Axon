// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_rendering_portable_validation : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    SemanticCommand command;
    string rules;

    void Establish()
    {
        var seed = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0];
        var property = seed.Properties[0] with { Name = "label", Type = new(SemanticTypeReferenceKind.Primitive, SemanticPrimitiveType.Text, default, false, false), IsIdentifier = false };
        command = seed with
        {
            Properties = [property],
            Validations = [new(property.Id, SemanticValidationRuleKind.Minimum, new SemanticNumberValue(3), "At least three characters."), new(property.Id, SemanticValidationRuleKind.Contains, new SemanticTextValue("\"quoted\""), null)],
            Requirements = [new(new SemanticComparison(new(property.Id, null), SemanticComparisonOperator.NotEqual, new(default, new SemanticTextValue("blocked"))), "Label is blocked.")]
        };
        context = new(request, new(request.Profile));
    }

    void Because() => rules = new CommandRulesEmitter(context, command, new(request.Model.Application)).Render("sample.commands");

    [Fact] void should_use_jakarta_validation() => rules.ShouldContain("import jakarta.validation.Validator;");
    [Fact] void should_preserve_optional_rule_null_semantics() => rules.ShouldContain("command.label() == null || (command.label().length() >= 3)");
    [Fact] void should_escape_text_operands() => rules.ShouldContain("command.label().contains(\"\\\"quoted\\\"\")");
    [Fact] void should_preserve_requirement_messages() => rules.ShouldContain("Label is blocked.");
    [Fact] void should_check_requirements() => rules.ShouldContain("!(java.util.Objects.equals(command.label(), \"blocked\"))");
    [Fact] void should_throw_after_collecting_rejections() => rules.ShouldContain("validation.throwIfInvalid();");
    [Fact] void should_not_degrade_supported_rules() => context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
}
