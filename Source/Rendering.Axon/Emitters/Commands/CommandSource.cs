// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed record CommandSource
{
    internal required (string ComponentName, string JavaType, string TagKey) Primary { get; init; }

    internal required SemanticTypeReference Type { get; init; }

    internal SemanticProperty? Input { get; init; }

    internal SemanticExpression? Destination { get; init; }

    internal string Member => Primary.ComponentName;

    internal string TagKey => Primary.TagKey;

    internal string JavaType => Primary.JavaType;

    internal bool RuntimeId => Type.Kind == SemanticTypeReferenceKind.Primitive;

    internal bool Allocated => Input is null && Destination is null;

    internal bool Generated => Input?.IsGenerated == true || Allocated;

    internal static CommandSource? Create(AxonEmissionContext context, SemanticCommand command, SemanticEventContract contract)
    {
        var tag = AxonEventSources.PrimaryTag(context, contract);
        var types = new CommandJavaTypes(context.Request.Model.Application);
        var type = TypeFor(context, tag.JavaType);
        var destination = command.Produces.FirstOrDefault()?.Destination ?? command.Destination?.Value;
        var input = InputProperty(command, contract, tag.ComponentName, destination);
        if (!CanIdentify(command, destination, input)) return null;
        var source = new CommandSource { Primary = tag, Type = type, Input = input, Destination = destination };
        if (source.ConflictsWithInput(command, types)) return null;
        return source.Value(command, types, "command") is null ? null : source;
    }

    internal static SemanticTypeReference TypeFor(AxonEmissionContext context, string javaType)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application);
        var concept = context.Request.Model.Application.Concepts.FirstOrDefault(concept => types.Type(SemanticTypeReference.ForConcept(concept.Id)) == javaType);
        return concept is null ? SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.Text) : SemanticTypeReference.ForConcept(concept.Id);
    }

    internal string? Value(SemanticCommand command, CommandJavaTypes types, string receiver)
    {
        if (Allocated) return receiver + "." + Member + "()";
        var value = ExpressionValue(new(command, types), types, receiver);
        if (value is null) return null;
        return RuntimeId ? $"new {JavaType}({value})" : value;
    }

    internal bool NeedsAccessor(SemanticCommand command) => !Allocated && !command.Properties.Any(property => JavaNames.Member(property.Name) == Member);

    static bool CanIdentify(SemanticCommand command, SemanticExpression? destination, SemanticProperty? input)
    {
        if (input is not null) return true;
        return destination is SemanticValueExpression || command.Destination is not null;
    }

    static SemanticProperty? InputProperty(SemanticCommand command, SemanticEventContract contract, string member, SemanticExpression? destination)
    {
        if (destination is SemanticValueExpression) return null;
        if (destination is null && command.Destination is not null) return null;
        if (destination is SemanticResolvedExpression reference) return command.Properties.SingleOrDefault(property => property.Id == reference.Target);
        var modeled = contract.Properties.FirstOrDefault(property => JavaNames.Member(property.Name) == member);
        var mapping = command.Produces.FirstOrDefault()?.Mappings.FirstOrDefault(mapping => mapping.TargetProperty == modeled?.Id);
        if (mapping?.Source is SemanticResolvedExpression mapped) return command.Properties.SingleOrDefault(property => property.Id == mapped.Target);
        return command.Properties.FirstOrDefault(property => property.IsIdentifier);
    }

    string? ExpressionValue(CommandExpressions expressions, CommandJavaTypes types, string receiver) => Input switch
    {
        { } input => expressions.Convert(receiver + "." + JavaNames.Member(input.Name) + "()", input.Type, Type),
        _ when Destination is SemanticValueExpression literal => types.Literal(literal.Value, Type),
        _ => null
    };

    bool ConflictsWithInput(SemanticCommand command, CommandJavaTypes types)
    {
        if (Input is not null) return JavaNames.Member(Input.Name) == Member && JavaType != types.Type(Input.Type);
        if (Destination is not null) return false;
        return command.Properties.Any(property => JavaNames.Member(property.Name) == Member);
    }
}
