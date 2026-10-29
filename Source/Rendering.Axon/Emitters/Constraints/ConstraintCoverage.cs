// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
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

    /// <summary>The constraints that apply to the events of a command but are not enforced by it, with the reason.</summary>
    internal static IReadOnlyList<(SemanticConstraint Constraint, string Reason)> NotEnforced(AxonEmissionContext context, SemanticCommand command, IReadOnlyList<ConstraintClaim> claims) =>
        [.. All(context).Select(item => item.Constraint).Where(constraint => command.Produces.Any(produced => Applies(constraint, produced.EventContract)))
            .Select(constraint => (Constraint: constraint, Reason: Reason(context, constraint) ?? (claims.Any(claim => claim.Constraint == constraint && claim.Binding is null) ? "the claimed value or owner is not mapped from a command property" : null)))
            .Where(item => item.Reason is not null).Select(item => (item.Constraint, item.Reason!))];

    internal static IEnumerable<(SemanticId Slice, SemanticConstraint Constraint)> All(AxonEmissionContext context) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Constraints.Select(constraint => (slice.Slice.Id, constraint)));

    internal static bool Applies(SemanticConstraint constraint, SemanticId contract) =>
        constraint.Targets.Any(target => target.EventContract == contract) || constraint.ReleasedBy.Contains(contract);

    internal static bool Supported(AxonEmissionContext context, SemanticConstraint constraint) => Reason(context, constraint) is null;

    /// <summary>The supported property-uniqueness constraints claimed by an event, with the event component that carries the value.</summary>
    internal static IEnumerable<(SemanticConstraint Constraint, string? Value, string ValueType)> EventClaims(AxonEmissionContext context, SemanticEventContract contract)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application, context.BasePackage);
        foreach (var (_, constraint) in All(context).Where(item => item.Constraint.Targets.Any(target => target.EventContract == contract.Id) && Supported(context, item.Constraint)))
        {
            if (constraint.Kind == SemanticConstraintKind.UniqueEventOccurrence)
            {
                yield return (constraint, null, AxonEventSources.PrimaryTag(context, contract).JavaType);
                continue;
            }
            var value = ValueProperty(context, constraint, contract)!;
            yield return (constraint, JavaNames.Member(value.Name), types.Type(value.Type));
        }
    }

    /// <summary>Why a constraint cannot render as a set-validation entity, or <c>null</c> when it can.</summary>
    internal static string? Reason(AxonEmissionContext context, SemanticConstraint constraint)
    {
        if (constraint.Scope != SemanticConstraintScope.EventSequence || constraint.Targets.IsEmpty) return "only event-sequence constraints with at least one target are supported";
        return constraint.Kind switch
        {
            SemanticConstraintKind.UniquePropertyValue => PropertyReason(context, constraint),
            SemanticConstraintKind.UniqueEventOccurrence => OccurrenceReason(context, constraint),
            _ => "only unique property values and unique event occurrences are supported"
        };
    }

    /// <summary>The supported constraints whose targets include an event the command produces, with the binding that enforces them.</summary>
    internal static IReadOnlyList<ConstraintClaim> Claims(AxonEmissionContext context, SemanticCommand command, CommandSource? source)
    {
        var claims = All(context).Where(item => Supported(context, item.Constraint)).SelectMany(item => command.Produces
            .Where(produced => item.Constraint.Targets.Any(target => target.EventContract == produced.EventContract))
            .Select(produced => new ConstraintClaim(item.Constraint, produced, Binding(context, command, source, item.Constraint, produced)))).ToArray();
        var ambiguous = claims.Where(claim => claim.Binding is not null).GroupBy(claim => claim.Constraint.Name)
            .Where(group => group.Select(claim => claim.Binding!.Property).Distinct(StringComparer.Ordinal).Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        return [.. claims.Select(claim => claim.Binding is { } binding && ambiguous.Contains(claim.Constraint.Name)
            ? claim with { Binding = binding with { Local = binding.Local + char.ToUpperInvariant(binding.Property[0]) + binding.Property[1..] } }
            : claim)];
    }

    /// <summary>The command property, owner expression and value type that enforce a claim, or <c>null</c> when they cannot be derived.</summary>
    internal static ConstraintBinding? Binding(AxonEmissionContext context, SemanticCommand command, CommandSource? source, SemanticConstraint constraint, SemanticProducedEvent produced)
    {
        if (source?.Generated != false) return null;
        var types = new CommandJavaTypes(context.Request.Model.Application, context.BasePackage);
        var owner = "command." + source.Member + "()";
        if (constraint.Kind == SemanticConstraintKind.UniqueEventOccurrence) return new(constraint, source.Member, owner, source.JavaType, string.Empty);
        var contract = CommandAppends.Contract(context, produced.EventContract);
        var target = constraint.Targets.First(candidate => candidate.EventContract == produced.EventContract);
        var property = contract.Properties.Single(candidate => candidate.Id == target.Properties.Single());
        var mapping = produced.Mappings.FirstOrDefault(candidate => candidate.TargetProperty == property.Id);
        if (mapping?.Source is not SemanticResolvedExpression { Root: SemanticExpressionRootKind.Command, Source: SemanticExpressionSourceKind.Property } reference) return null;
        var input = command.Properties.SingleOrDefault(candidate => candidate.Id == reference.Target);
        if (input is null || types.Type(input.Type) != types.Type(property.Type)) return null;
        return new(constraint, JavaNames.Member(input.Name), owner, types.Type(property.Type), JavaNames.Member(property.Name));
    }

    /// <summary>The value component of a property-uniqueness constraint on an event, when the event claims or releases it.</summary>
    internal static IEnumerable<(SemanticConstraint Constraint, SemanticProperty Property)> Tagged(AxonEmissionContext context, SemanticEventContract contract) =>
        All(context).Select(item => item.Constraint).Where(constraint => constraint.Kind == SemanticConstraintKind.UniquePropertyValue && Applies(constraint, contract.Id) && Supported(context, constraint))
            .Select(constraint => (Constraint: constraint, Property: ValueProperty(context, constraint, contract)))
            .Where(item => item.Property is not null).Select(item => (item.Constraint, item.Property!));

    /// <summary>The Java expression that normalizes a unique value for its tag, or <c>null</c> when the value is tagged as is. Case-insensitive text is lower-cased and decimals drop trailing zeros, because Axon compares tags as strings.</summary>
    internal static string? Normalized(AxonEmissionContext context, SemanticConstraint constraint, SemanticTypeReference type, string expression)
    {
        var types = new CommandJavaTypes(context.Request.Model.Application, context.BasePackage);
        if (constraint.IgnoreCasing) return expression + ".toLowerCase(java.util.Locale.ROOT)";
        return type.IsCollection || types.PrimitiveOf(type) != SemanticPrimitiveType.DecimalNumber ? null : types.Unwrap(expression, type) + ".stripTrailingZeros().toPlainString()";
    }

    internal static SemanticProperty? ValueProperty(AxonEmissionContext context, SemanticConstraint constraint, SemanticEventContract contract)
    {
        var name = Contract(context, constraint.Targets[0].EventContract)?.Properties.FirstOrDefault(property => property.Id == constraint.Targets[0].Properties.FirstOrDefault())?.Name;
        return contract.Properties.FirstOrDefault(property => property.Name == name);
    }

    internal static SemanticEventContract? Contract(AxonEmissionContext context, SemanticId id) =>
        ScopeSelection.Locate(context.Request.Model.Application).SelectMany(slice => slice.Slice.Events).FirstOrDefault(contract => contract.Id == id);

    /// <summary>The primary tag of an event, or <c>null</c> when its owner identity cannot be resolved.</summary>
    internal static (string ComponentName, string JavaType, string TagKey)? Owner(AxonEmissionContext context, SemanticId id)
    {
        var contract = Contract(context, id);
        if (contract is null) return null;
        try
        {
            var primary = AxonEventSources.PrimaryTag(context, contract);
            return string.IsNullOrEmpty(primary.ComponentName) || string.IsNullOrEmpty(primary.JavaType) ? null : primary;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    static string? PropertyReason(AxonEmissionContext context, SemanticConstraint constraint)
    {
        if (constraint.Targets.Any(target => target.Properties.Length != 1)) return "composite or empty unique values are not supported";
        if (!constraint.Targets.All(target => TargetSupported(context, target))) return "the unique value is not a scalar property of its event";
        var types = new CommandJavaTypes(context.Request.Model.Application, context.BasePackage);
        var values = constraint.Targets.Select(target => Contract(context, target.EventContract)!.Properties.Single(property => property.Id == target.Properties[0])).ToArray();
        if (values.Any(value => value.Name != values[0].Name || types.Type(value.Type) != types.Type(values[0].Type))) return "the claiming events disagree on the name or type of the unique value";
        if (constraint.IgnoreCasing && (values[0].Type.Kind != SemanticTypeReferenceKind.Primitive || values[0].Type.Primitive != SemanticPrimitiveType.Text)) return "ignoring case is only supported for text values";
        foreach (var id in constraint.ReleasedBy)
        {
            var released = Contract(context, id)?.Properties.FirstOrDefault(property => property.Name == values[0].Name);
            if (released is null || types.Type(released.Type) != types.Type(values[0].Type)) return "a release event does not carry the unique value";
        }
        return OwnerReason(context, constraint);
    }

    static string? OccurrenceReason(AxonEmissionContext context, SemanticConstraint constraint)
    {
        var supported = constraint.Targets.All(target => target.Properties.IsEmpty && OccurrenceOwnerSupported(context, target.EventContract)) &&
            constraint.ReleasedBy.All(id => OccurrenceOwnerSupported(context, id));
        return supported ? OwnerReason(context, constraint) : "the owner identity of the event is not an integral or text identity";
    }

    static string? OwnerReason(AxonEmissionContext context, SemanticConstraint constraint)
    {
        var owners = constraint.Targets.Select(target => target.EventContract).Concat(constraint.ReleasedBy).Select(id => Owner(context, id)).ToArray();
        if (owners.Any(owner => owner is null)) return "the owner identity of an event cannot be resolved";
        return owners.Select(owner => (owner!.Value.JavaType, owner.Value.TagKey)).Distinct().Count() == 1 ? null : "the claim and release events have different owner identities";
    }

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
