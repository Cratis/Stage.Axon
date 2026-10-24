// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Constraints;

internal static class ConstraintCoverage
{
    internal static bool CommandRequiresUnsupportedConstraint(AxonEmissionContext context, SemanticCommand command) =>
        command.Produces.Any(produced => EventRequiresUnsupportedConstraint(context, produced.EventContract));

    internal static bool EventRequiresUnsupportedConstraint(AxonEmissionContext context, SemanticId contract) =>
        All(context).Any(item => Applies(item.Constraint, contract) && !Supported(context, item.Constraint));

    internal static IEnumerable<(SemanticId Slice, SemanticConstraint Constraint)> All(AxonEmissionContext context) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Constraints.Select(constraint => (slice.Slice.Id, constraint)));

    internal static bool Applies(SemanticConstraint constraint, SemanticId contract) =>
        constraint.Targets.Any(target => target.EventContract == contract) || constraint.ReleasedBy.Contains(contract);

    internal static bool Supported(AxonEmissionContext context, SemanticConstraint constraint) =>
        constraint.Kind == SemanticConstraintKind.UniquePropertyValue && constraint.Scope == SemanticConstraintScope.EventSequence &&
        !constraint.Targets.IsEmpty && constraint.Targets.All(target => TargetSupported(context, target));

    static bool TargetSupported(AxonEmissionContext context, SemanticConstraintTarget target)
    {
        var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).FirstOrDefault(contract => contract.Id == target.EventContract);
        return contract is not null && !target.Properties.IsEmpty && target.Properties.All(id =>
            contract.Properties.Any(property => property.Id == id && Scalar(context, property.Type)));
    }

    static bool Scalar(AxonEmissionContext context, SemanticTypeReference type) => type.Kind switch
    {
        SemanticTypeReferenceKind.Primitive => true,
        SemanticTypeReferenceKind.Concept => context.Request.Model.Application.Concepts.Any(concept => concept.Id == type.Target && concept.Values.IsDefaultOrEmpty),
        _ => false
    };
}
