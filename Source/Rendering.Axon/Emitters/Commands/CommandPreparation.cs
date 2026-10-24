// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandPreparation(AxonEmissionContext context, SemanticCommand command, CommandSource? source)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application);

    internal void Emit(JavaCodeBuilder body)
    {
        if (!command.Properties.Any(property => property.IsGenerated) && source?.Allocated != true) return;
        var name = JavaNames.Type(command.Name);
        var inputs = command.Properties.Select(property => property.IsGenerated ? "null" : JavaNames.Member(property.Name) + "()").ToList();
        if (source?.Allocated == true) inputs.Insert(0, "null");
        body.Line($"public {name} forRequest() {{ return new {name}({string.Join(", ", inputs)}); }}")
            .Block($"public {name} withGenerated(java.time.Clock clock)", Generated);
    }

    void Generated(JavaCodeBuilder body)
    {
        var values = command.Properties.Select(property => property.IsGenerated ? Generate(property) : JavaNames.Member(property.Name) + "()").ToList();
        if (source?.Allocated == true) values.Insert(0, Allocated(source));
        if (values.Exists(value => value is null))
        {
            const string intent = "generate the command's modeled values";
            var path = command.Id + "/properties/generation";
            context.Degradation.Unsupported(AxonRefusalCodes.Generation, command.Id, "The command uses an unsupported value-generation strategy.", intent, path);
            body.UnsupportedIntent(intent, path, AxonRefusalCodes.Generation).Line("throw new UnsupportedOperationException(" + JavaCodeBuilder.StringLiteral(AxonRefusalCodes.Generation + ": " + intent) + ");");
            return;
        }
        body.Line($"return new {JavaNames.Type(command.Name)}({string.Join(", ", values)});");
    }

    string? Generate(SemanticProperty property)
    {
        if (_types.PrimitiveOf(property.Type) != SemanticPrimitiveType.Uuid || property.Type.IsCollection) return null;
        var value = property.Type.Kind == SemanticTypeReferenceKind.Concept ? Concept(property) : "java.util.UUID.randomUUID()";
        var member = JavaNames.Member(property.Name) + "()";
        return value is null ? null : $"({member} == null ? {value} : {member})";
    }

    string? Allocated(CommandSource identity)
    {
        var primitive = _types.PrimitiveOf(identity.Type);
        var value = context.BasePackage + ".runtime.GenerateValue.uuid()";
        if (primitive == SemanticPrimitiveType.Text) value += ".toString()";
        else if (primitive != SemanticPrimitiveType.Uuid) return null;
        var wrapped = identity.RuntimeId ? $"new {identity.JavaType}({value})" : _types.Wrap(value, identity.Type);
        var member = identity.Member + "()";
        return wrapped is null ? null : $"({member} == null ? {wrapped} : {member})";
    }

    string? Concept(SemanticProperty property)
    {
        var concept = context.Request.Model.Application.Concepts.Single(concept => concept.Id == property.Type.Target);
        return string.Equals(concept.Generation?.Kind, "uuid", StringComparison.OrdinalIgnoreCase) ? _types.Type(property.Type) + ".create()" : null;
    }
}
