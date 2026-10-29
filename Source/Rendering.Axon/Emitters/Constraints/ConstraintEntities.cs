// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints;

/// <summary>Names of the generated set-validation entities.</summary>
internal static class ConstraintEntities
{
    internal static string Package(AxonEmissionContext context) => context.BasePackage + ".constraints";

    internal static string Type(SemanticConstraint constraint) => JavaNames.Type(constraint.Name) + "SetValidation";

    internal static string Qualified(AxonEmissionContext context, SemanticConstraint constraint) => Package(context) + "." + Type(constraint);

    internal static string Assert(SemanticConstraint constraint, string value) => constraint.Kind == SemanticConstraintKind.UniqueEventOccurrence
        ? "assert" + JavaNames.Type(constraint.Name) + "Unique"
        : "assert" + JavaNames.Type(value) + "Unique";

    internal static string Message(SemanticConstraint constraint) => constraint.Message ?? ("Constraint violated: " + constraint.Name);
}
