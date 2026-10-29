// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints;

/// <summary>A supported constraint claimed by an event that a command or reaction produces, with the binding that enforces it.</summary>
/// <param name="Constraint">The constraint.</param>
/// <param name="Produced">The produced event that claims it.</param>
/// <param name="Binding">The binding, or <c>null</c> when it cannot be derived.</param>
internal sealed record ConstraintClaim(SemanticConstraint Constraint, SemanticProducedEvent Produced, ConstraintBinding? Binding);

/// <summary>How a handler enforces one constraint: the command member that identifies the set-validation entity and the owner it asserts for.</summary>
/// <param name="Constraint">The constraint.</param>
/// <param name="Property">The command member that carries the entity id.</param>
/// <param name="Owner">The Java expression of the owner.</param>
/// <param name="ValueType">The Java type of the entity id.</param>
/// <param name="Value">The event component that carries the unique value, empty for occurrences.</param>
internal sealed record ConstraintBinding(SemanticConstraint Constraint, string Property, string Owner, string ValueType, string Value)
{
    internal bool Occurrence => Constraint.Kind == SemanticConstraintKind.UniqueEventOccurrence;

    internal string Entity => ConstraintEntities.Type(Constraint);

    internal string Assert => ConstraintEntities.Assert(Constraint, Value);

    /// <summary>The parameter name of the injected entity.</summary>
    internal string Local => JavaNames.Member(Constraint.Name);

    internal string Call => Occurrence ? Local + "." + Assert + "();" : Local + "." + Assert + "(" + Owner + ");";
}
