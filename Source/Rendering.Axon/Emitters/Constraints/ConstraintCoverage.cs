// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Java;
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
        constraint.Scope == SemanticConstraintScope.EventSequence && !constraint.Targets.IsEmpty && constraint.Kind switch
        {
            SemanticConstraintKind.UniquePropertyValue => constraint.Targets.All(target => TargetSupported(context, target)),
            SemanticConstraintKind.UniqueEventOccurrence => constraint.Targets.All(target => target.Properties.IsEmpty && OccurrenceOwnerSupported(context, target.EventContract)) &&
                constraint.ReleasedBy.All(id => OccurrenceOwnerSupported(context, id)),
            _ => false
        };

    static bool OccurrenceOwnerSupported(AxonEmissionContext context, SemanticId id)
    {
        var contract = ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).FirstOrDefault(contract => contract.Id == id);
        if (contract is null) return false;
        try
        {
            var primary = AxonEventSources.PrimaryTag(context, contract);
            return !string.IsNullOrEmpty(primary.ComponentName) && !string.IsNullOrEmpty(primary.JavaType) &&
                OccurrenceIdentitySupported(context, contract, primary.ComponentName, primary.JavaType);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    static bool OccurrenceIdentitySupported(AxonEmissionContext context, SemanticEventContract contract, string component, string javaType)
    {
        var property = contract.Properties.FirstOrDefault(property => JavaNames.Member(property.Name) == component);
        if (property is not null && NonIntegral(context, property.Type)) return false;
        return !context.Request.Model.Application.Concepts.Any(concept => concept.Primitive == SemanticPrimitiveType.DecimalNumber &&
            javaType == context.BasePackage + ".concepts." + JavaNames.Type(concept.Name));
    }

    static bool NonIntegral(AxonEmissionContext context, SemanticTypeReference type) => type.Kind switch
    {
        SemanticTypeReferenceKind.Primitive => type.Primitive == SemanticPrimitiveType.DecimalNumber,
        SemanticTypeReferenceKind.Concept => context.Request.Model.Application.Concepts.Any(concept => concept.Id == type.Target && concept.Primitive == SemanticPrimitiveType.DecimalNumber),
        _ => false
    };

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
