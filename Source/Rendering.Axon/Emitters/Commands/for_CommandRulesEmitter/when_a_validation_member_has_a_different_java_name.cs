// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandRulesEmitter;

public class when_a_validation_member_has_a_different_java_name : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string code;
    string unchanged;

    void Because()
    {
        var seed = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty).Slice.Commands[0];
        var property = seed.Properties[0] with { Name = "class", Type = SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text) };
        var command = seed with { Properties = [property], Validations = [new(property.Id, SemanticValidationRuleKind.NotEmpty, null, "Required")], Requirements = [] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        var types = new CommandJavaTypes(request.Model.Application, context.BasePackage);
        code = new CommandRulesEmitter(context, command, types).Render("example.commands");
        unchanged = new CommandRulesEmitter(context, command with { Properties = [property with { Name = "unchanged" }] }, types).Render("example.commands");
    }

    [Fact] void should_translate_bean_validation_members_in_both_validation_entry_points() => code.Split("validation.reject(validationMember(violation.getPropertyPath().toString()), violation.getMessage())").Length.ShouldEqual(3);
    [Fact] void should_map_the_java_member_to_the_modelled_name() => code.ShouldContain("case \"0:class_\" -> \"class\";");
    [Fact] void should_keep_rule_errors_on_the_modelled_name() => code.ShouldContain("validation.require((command.class_() != null && !command.class_().isBlank()), \"class\", \"Required\");");
    [Fact] void should_leave_unknown_segments_unchanged() => code.ShouldContain("default -> member;");
    [Fact] void should_not_emit_a_mapping_when_names_match() => unchanged.ShouldNotContain("validationMember");
    [Fact] void should_keep_the_original_rejection_when_names_match() => unchanged.ShouldContain("validation.reject(violation.getPropertyPath().toString(), violation.getMessage())");
}
