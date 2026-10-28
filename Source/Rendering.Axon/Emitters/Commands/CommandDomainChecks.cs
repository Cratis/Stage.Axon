// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
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

    /// <summary>Emits the inline checks; returns false when an unsupported rule ended the method with a fail-closed throw.</summary>
    /// <param name="builder">The handler method body.</param>
    /// <returns><c>true</c> when the body may continue; otherwise <c>false</c>.</returns>
    internal bool Emit(JavaCodeBuilder builder)
    {
        if (!HasRules) return true;
        var failures = builder.Reference("java.util.ArrayList");
        builder.Line($"var failures = new {failures}<{builder.Reference("java.lang.String")}>();");
        if (!RenderRules(builder)) return false;
        builder.Line($"if (!failures.isEmpty()) throw new {builder.Reference("java.lang.IllegalArgumentException")}({builder.Reference("java.lang.String")}.join(\"; \", failures));");
        return true;
    }

    bool RenderRules(JavaCodeBuilder builder)
    {
        if (!RenderPropertyRules(builder) || !RenderConceptRules(builder) || !RenderRequirements(builder)) return false;
        if (command.CodeValidations.IsEmpty) return true;
        Unsupported(builder, "run attached command validation code");
        return false;
    }

    SemanticConcept Concept(SemanticProperty property) => context.Request.Model.Application.Concepts.Single(concept => concept.Id == property.Type.Target);

    void ClaimConceptRules(JavaCodeBuilder builder, SemanticProperty property)
    {
        foreach (var rule in Concept(property).Validations)
        {
            new CommandValidationPredicates(types, rule, property.Type, string.Empty).Claim(builder);
        }
    }

    bool RenderPropertyRules(JavaCodeBuilder builder)
    {
        foreach (var rule in command.Validations)
        {
            var property = command.Properties.SingleOrDefault(property => property.Id == rule.Property);
            if (property is null)
            {
                Unsupported(builder, "validate an unresolved property");
                return false;
            }
            if (!RenderRule(builder, property, rule)) return false;
        }
        return true;
    }

    bool RenderConceptRules(JavaCodeBuilder builder)
    {
        foreach (var property in command.Properties.Where(property => property.Type.Kind == SemanticTypeReferenceKind.Concept))
        {
            foreach (var rule in Concept(property).Validations)
            {
                if (!RenderRule(builder, property, rule)) return false;
            }
        }
        return true;
    }

    bool RenderRequirements(JavaCodeBuilder builder)
    {
        foreach (var requirement in command.Requirements)
        {
            var condition = new CommandConditions(command, types).Render(requirement.Condition, builder);
            if (condition is null)
            {
                Unsupported(builder, "evaluate a command requirement using protected state");
                return false;
            }
            builder.Line($"if (!({condition})) failures.add({JavaCodeBuilder.StringLiteral(requirement.Message ?? "Command requirement was not satisfied.")});");
        }
        return true;
    }

    bool RenderRule(JavaCodeBuilder builder, SemanticProperty property, SemanticValidationRule rule)
    {
        var member = JavaNames.Member(property.Name);
        var predicate = new CommandValidationPredicates(types, rule, property.Type, $"command.{member}()").Render(builder);
        if (predicate is null)
        {
            Unsupported(builder, $"validate {property.Name} using {rule.Kind}");
            return false;
        }
        builder.Line($"if (!({predicate})) failures.add({JavaCodeBuilder.StringLiteral(rule.Message ?? (property.Name + " failed " + rule.Kind + "."))});");
        return true;
    }

    void Unsupported(JavaCodeBuilder builder, string intent)
    {
        var path = command.Id + "/validation";
        context.Degradation.Unsupported(AxonRefusalCodes.CommandValidation, command.Id, $"Axon cannot yet {intent}.", intent, path);

        // Keep unsupported guards fail-closed: a warning is not permission to execute an unvalidated command.
        builder.UnsupportedIntent(intent, path, AxonRefusalCodes.CommandValidation)
            .Line($"throw new {builder.Reference("java.lang.UnsupportedOperationException")}({JavaCodeBuilder.StringLiteral(AxonRefusalCodes.CommandValidation + ": " + intent)});");
    }
}
