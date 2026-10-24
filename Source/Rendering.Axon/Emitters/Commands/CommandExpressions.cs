// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands;

internal sealed class CommandExpressions(SemanticCommand command, CommandJavaTypes types)
{
    internal string? Render(SemanticExpression expression, SemanticTypeReference target) => expression switch
    {
        SemanticValueExpression literal => types.Literal(literal.Value, target),
        SemanticResolvedExpression { Root: SemanticExpressionRootKind.Command, Source: SemanticExpressionSourceKind.Property } reference => Property(reference, target),
        _ => null
    };

    internal string? Convert(string expression, SemanticTypeReference source, SemanticTypeReference target)
    {
        if (SameType(source, target)) return expression;
        if (source.IsCollection || target.IsCollection) return null;
        if (types.PrimitiveOf(source) != types.PrimitiveOf(target)) return null;
        var value = types.Unwrap(expression, source);
        if (target.Kind == SemanticTypeReferenceKind.Primitive) return value;
        if (target.Kind != SemanticTypeReferenceKind.Concept) return null;
        return types.Wrap(value, target);
    }

    static bool SameType(SemanticTypeReference source, SemanticTypeReference target) =>
        source.Kind == target.Kind && source.Target == target.Target && source.Primitive == target.Primitive;

    string? Property(SemanticResolvedExpression reference, SemanticTypeReference target)
    {
        var property = command.Properties.SingleOrDefault(property => property.Id == reference.Target);
        return property is null ? null : Convert("command." + JavaNames.Member(property.Name) + "()", property.Type, target);
    }
}
