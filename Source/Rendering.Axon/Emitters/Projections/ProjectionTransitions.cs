// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal static class ProjectionTransitions
{
    internal static IEnumerable<SemanticProjectionFrom> From(SemanticProjection projection) => projection.Scope?.From ??
        [.. projection.Transitions.Select(transition => new SemanticProjectionFrom(
            transition.EventContract,
            new SemanticProjectionValueKey(Value(transition.AffectedInstance.Key)),
            null,
            [.. transition.Mappings.Select(mapping => new SemanticProjectionMapping([mapping.TargetProperty], SemanticProjectionOperation.Set, Value(mapping.Source)))]))];

    static SemanticProjectionValue Value(SemanticExpression expression) => expression switch
    {
        SemanticResolvedExpression { Root: SemanticExpressionRootKind.Event, Source: SemanticExpressionSourceKind.Property } property => new SemanticProjectionEventProperty([property.Target]),
        SemanticValueExpression literal => new SemanticProjectionLiteral(literal.Value),
        _ => new SemanticProjectionEventContextValue("unsupported")
    };
}
