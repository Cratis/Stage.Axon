// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandRulesEmitter(AxonEmissionContext context, SemanticCommand command, CommandJavaTypes types)
{
    internal string Render(string package)
    {
        var name = JavaNames.Type(command.Name);
        var builder = new JavaCodeBuilder(package)
            .Import("org.springframework.stereotype.Component")
            .Import("jakarta.validation.Validator")
            .Import(types.BasePackage + ".runtime.ValidationContext");
        builder.Line("@Component").Block($"public final class {name}Rules", body =>
            body.Line("private final Validator validator;").Line()
                .Block($"public {name}Rules(Validator validator)", constructor => constructor.Line("this.validator = validator;")).Line()
                .Block($"public void validate({name} command)", RenderValidation));
        return builder.Build();
    }

    static string Safe(string value) => value.Replace('\\', '/').Replace('\r', ' ').Replace('\n', ' ');

    void RenderValidation(JavaCodeBuilder builder)
    {
        builder.Line("var validation = new ValidationContext();").Line("validator.validate(command).stream().sorted(java.util.Comparator.comparing(violation -> violation.getPropertyPath().toString())).forEach(violation -> validation.reject(violation.getPropertyPath().toString(), violation.getMessage()));");
        RenderPropertyRules(builder);
        RenderConceptRules(builder);
        RenderRequirements(builder);
        if (!command.CodeValidations.IsEmpty) Unsupported(builder, "run attached command validation code");
        builder.Line("validation.throwIfInvalid();");
    }

    void RenderPropertyRules(JavaCodeBuilder builder)
    {
        foreach (var rule in command.Validations)
        {
            var property = command.Properties.SingleOrDefault(property => property.Id == rule.Property);
            if (property is null) Unsupported(builder, "validate an unresolved property");
            else RenderRule(builder, property, rule);
        }
    }

    void RenderConceptRules(JavaCodeBuilder builder)
    {
        foreach (var property in command.Properties.Where(property => property.Type.Kind == SemanticTypeReferenceKind.Concept))
        {
            var concept = context.Request.Model.Application.Concepts.Single(concept => concept.Id == property.Type.Target);
            foreach (var rule in concept.Validations) RenderRule(builder, property, rule);
        }
    }

    void RenderRequirements(JavaCodeBuilder builder)
    {
        foreach (var requirement in command.Requirements)
        {
            var condition = new CommandConditions(command, types).Render(requirement.Condition);
            if (condition is null) Unsupported(builder, "evaluate a command requirement using protected state");
            else builder.Line($"validation.require({condition}, \"\", {JavaCodeBuilder.StringLiteral(requirement.Message ?? "Command requirement was not satisfied.")});");
        }
    }

    void RenderRule(JavaCodeBuilder builder, SemanticProperty property, SemanticValidationRule rule)
    {
        var member = JavaNames.Member(property.Name);
        var predicate = new CommandValidationPredicates(types, rule, property.Type, $"command.{member}()").Render();
        if (predicate is null)
        {
            Unsupported(builder, $"validate {property.Name} using {rule.Kind}");
            return;
        }
        builder.Line($"validation.require({predicate}, {JavaCodeBuilder.StringLiteral(property.Name)}, {JavaCodeBuilder.StringLiteral(rule.Message ?? (property.Name + " failed " + rule.Kind + "."))});");
    }

    void Unsupported(JavaCodeBuilder builder, string intent)
    {
        context.Degradation.Unsupported(AxonRefusalCodes.CommandValidation, command.Id, $"Axon cannot yet {intent}.");

        // Keep unsupported guards fail-closed: a warning is not permission to execute an unvalidated command.
        builder.Line($"// Screenplay wanted to {Safe(intent)} ({Safe(command.Id.ToString())}), but this is not supported by the Axon target yet ({AxonRefusalCodes.CommandValidation}).").Line($"validation.reject(\"\", {JavaCodeBuilder.StringLiteral(AxonRefusalCodes.CommandValidation + ": " + intent)});");
    }
}
