// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

/// <summary>
/// Lowers modeled command rules and requirements to inline checks inside the command handler.
/// Structural validation is not handled here: Bean Validation annotations on the command record are enforced by the native dispatch interceptor.
/// </summary>
/// <param name="context">The emission context.</param>
/// <param name="command">The command whose modeled rules are lowered.</param>
/// <param name="types">The Java type mapper.</param>
internal sealed class CommandDomainChecks(AxonEmissionContext context, SemanticCommand command, CommandJavaTypes types)
{
    bool HasRules => !command.Validations.IsEmpty || !command.Requirements.IsEmpty || !command.CodeValidations.IsEmpty || HasConceptRules;

    bool HasConceptRules => command.Properties.Any(property => property.Type.Kind == SemanticTypeReferenceKind.Concept && !Concept(property).Validations.IsEmpty);

    internal void Claim(JavaCodeBuilder builder)
    {
        foreach (var rule in command.Validations)
        {
            if (command.Properties.SingleOrDefault(property => property.Id == rule.Property) is { } property) new CommandValidationPredicates(types, rule, property.Type, string.Empty).Claim(builder);
        }
        foreach (var property in command.Properties.Where(property => property.Type.Kind == SemanticTypeReferenceKind.Concept)) ClaimConceptRules(builder, property);
    }

    /// <summary>Emits the inline checks; returns whether the body may continue; unimplemented rules degrade to documented no-ops, so it is always true.</summary>
    /// <param name="builder">The handler method body.</param>
    /// <returns><c>true</c> when the body may continue; otherwise <c>false</c>.</returns>
    internal bool Emit(JavaCodeBuilder builder)
    {
        if (!HasRules) return true;
        var failures = builder.Reference("java.util.ArrayList");
        builder.Line($"var failures = new {failures}<{builder.Reference("java.lang.String")}>();");
        RenderRules(builder);
        builder.Line($"if (!failures.isEmpty()) throw new {builder.Reference("java.lang.IllegalArgumentException")}({builder.Reference("java.lang.String")}.join(\"; \", failures));");
        return true;
    }

    void RenderRules(JavaCodeBuilder builder)
    {
        RenderPropertyRules(builder);
        RenderConceptRules(builder);
        RenderRequirements(builder);
        if (!command.CodeValidations.IsEmpty) Unsupported(builder, "run attached command validation code");
    }

    SemanticConcept Concept(SemanticProperty property) => context.Request.Model.Application.Concepts.Single(concept => concept.Id == property.Type.Target);

    void ClaimConceptRules(JavaCodeBuilder builder, SemanticProperty property)
    {
        foreach (var rule in Concept(property).Validations)
        {
            new CommandValidationPredicates(types, rule, property.Type, string.Empty).Claim(builder);
        }
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
            foreach (var rule in Concept(property).Validations)
            {
                RenderRule(builder, property, rule);
            }
        }
    }

    void RenderRequirements(JavaCodeBuilder builder)
    {
        foreach (var requirement in command.Requirements)
        {
            var condition = new CommandConditions(command, types).Render(requirement.Condition, builder);
            if (condition is null) Unsupported(builder, "evaluate a command requirement using protected state");
            else builder.Line($"if (!({condition})) failures.add({JavaCodeBuilder.StringLiteral(requirement.Message ?? "Command requirement was not satisfied.")});");
        }
    }

    void RenderRule(JavaCodeBuilder builder, SemanticProperty property, SemanticValidationRule rule)
    {
        var member = JavaNames.Member(property.Name);
        var predicate = new CommandValidationPredicates(types, rule, property.Type, $"command.{member}()").Render(builder);
        if (predicate is null)
        {
            Unsupported(builder, $"validate {property.Name} using {rule.Kind}");
            return;
        }
        builder.Line($"if (!({predicate})) failures.add({JavaCodeBuilder.StringLiteral(rule.Message ?? (property.Name + " failed " + rule.Kind + "."))});");
    }

    void Unsupported(JavaCodeBuilder builder, string intent)
    {
        var path = command.Id + "/validation";
        builder.DiffersFromScreenplay(UnimplementedValidation.Report(context, command.Id, intent, path))
            .UnsupportedIntent(intent, path, AxonRefusalCodes.ImplementationValidation);
    }
}
