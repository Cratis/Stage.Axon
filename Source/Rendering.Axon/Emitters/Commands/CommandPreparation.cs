// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandPreparation(AxonEmissionContext context, SemanticCommand command, CommandSource? source)
{
    readonly CommandJavaTypes _types = new(context.Request.Model.Application, context.BasePackage);

    internal void Emit(JavaCodeBuilder body)
    {
        if (!command.Properties.Any(property => property.IsGenerated) && source?.Allocated != true) return;
        var name = JavaNames.Type(command.Name);
        ClaimTypes(body);
        var inputs = command.Properties.Select(property => property.IsGenerated ? "null" : JavaNames.Member(property.Name) + "()").ToList();
        if (source?.Allocated == true) inputs.Insert(0, "null");
        body.Line($"public {name} forRequest() {{ return new {name}({string.Join(", ", inputs)}); }}")
            .Block($"public {name} withGenerated({body.Reference("java.time.Clock")} clock)", Generated);
    }

    void ClaimTypes(JavaCodeBuilder body)
    {
        body.Declare(JavaNames.Type(command.Name));
        var located = ScopeSelection.Locate(context.Request.Model.Application).FirstOrDefault(slice => slice.Slice.Commands.Any(candidate => candidate.Id == command.Id));
        if (located is not null)
            foreach (var candidate in located.Slice.Commands) body.Declare(JavaNames.Type(candidate.Name));
        if (!CanPrepare()) return;
        foreach (var property in command.Properties.Where(property => property.IsGenerated)) JavaSemanticTypes.Claim(context, property.Type, body);
        if (source?.Allocated != true) return;
        if (source.RuntimeId) body.Reference(source.JavaType);
        else JavaSemanticTypes.Claim(context, source.Type, body);
    }

    void Generated(JavaCodeBuilder body)
    {
        if (!CanPrepare())
        {
            const string intent = "generate the command's modeled values";
            var path = command.Id + "/properties/generation";
            context.Degradation.Unsupported(AxonRefusalCodes.Generation, command.Id, "The command uses an unsupported value-generation strategy.", intent, path);
            body.UnsupportedIntent(intent, path, AxonRefusalCodes.Generation).Line($"throw new {body.Reference("java.lang.UnsupportedOperationException")}(" + JavaCodeBuilder.StringLiteral(AxonRefusalCodes.Generation + ": " + intent) + ");");
            return;
        }
        var values = command.Properties.Select(property => property.IsGenerated ? Generate(property, body) : JavaNames.Member(property.Name) + "()").ToList();
        if (source?.Allocated == true) values.Insert(0, Allocated(source, body));
        body.Line($"return new {JavaNames.Type(command.Name)}({string.Join(", ", values)});");
    }

    bool CanPrepare() => command.Properties.Where(property => property.IsGenerated).All(CanGenerate) &&
        (source?.Allocated != true || CanAllocate(source));

    bool CanGenerate(SemanticProperty property) => _types.PrimitiveOf(property.Type) == SemanticPrimitiveType.Uuid && !property.Type.IsCollection &&
        (property.Type.Kind != SemanticTypeReferenceKind.Concept || string.Equals(Concept(property.Type).Generation?.Kind, "uuid", StringComparison.OrdinalIgnoreCase));

    bool CanAllocate(CommandSource identity) => _types.PrimitiveOf(identity.Type) is SemanticPrimitiveType.Text or SemanticPrimitiveType.Uuid &&
        (identity.RuntimeId || Concept(identity.Type).Numbers is null);

    string Generate(SemanticProperty property, JavaCodeBuilder body)
    {
        var type = JavaSemanticTypes.TypeFor(context, property.Type, body);
        var value = property.Type.Kind == SemanticTypeReferenceKind.Concept ? type + ".create()" : type + ".randomUUID()";
        var member = JavaNames.Member(property.Name) + "()";
        return $"({member} == null ? {value} : {member})";
    }

    string Allocated(CommandSource identity, JavaCodeBuilder body)
    {
        var value = body.Reference(CratisFundamentals.ConceptsPackage + ".GenerateValue") + ".uuid()";
        if (_types.PrimitiveOf(identity.Type) == SemanticPrimitiveType.Text) value += ".toString()";
        var type = identity.RuntimeId ? body.Reference(identity.JavaType) : JavaSemanticTypes.TypeFor(context, identity.Type, body);
        var wrapped = identity.RuntimeId || Concept(identity.Type).Values.IsDefaultOrEmpty ? $"new {type}({value})" : $"{type}.fromValue({value})";
        var member = identity.Member + "()";
        return $"({member} == null ? {wrapped} : {member})";
    }

    SemanticConcept Concept(SemanticTypeReference type) => context.Request.Model.Application.Concepts.Single(concept => concept.Id == type.Target);
}
