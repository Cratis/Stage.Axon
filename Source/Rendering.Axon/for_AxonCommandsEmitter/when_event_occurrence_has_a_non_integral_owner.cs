// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Constraints;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_event_occurrence_has_a_non_integral_owner : when_a_native_handler_is_constrained
{
    protected override SemanticConstraintKind Kind => SemanticConstraintKind.UniqueEventOccurrence;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void should_fail_closed_for_decimal_concepts_and_components(bool primitive, bool strict)
    {
        ChangeOwner(SemanticPrimitiveType.DecimalNumber, primitive);
        AssertRefused(strict, "enforce or release modeled uniqueness in the append unit of work");
    }

    [Fact]
    void should_keep_integral_identities_supported()
    {
        ChangeOwner(SemanticPrimitiveType.WholeNumber, false);
        var context = new AxonEmissionContext(request, new(request.Profile));
        ConstraintCoverage.Supported(context, constraint).ShouldBeTrue();
    }

    [Fact]
    void should_not_change_property_uniqueness()
    {
        ChangeOwner(SemanticPrimitiveType.DecimalNumber, false);
        var contract = located.Slice.Events.Single(contract => contract.Id == constraint.Targets[0].EventContract);
        var property = contract.Properties.Single(property => property.Name == "customer");
        var context = new AxonEmissionContext(request, new(request.Profile));
        ConstraintCoverage.Supported(context, constraint with { Kind = SemanticConstraintKind.UniquePropertyValue, Targets = [new(contract.Id, [property.Id])] }).ShouldBeTrue();
    }

    void ChangeOwner(SemanticPrimitiveType type, bool primitive)
    {
        var application = request.Model.Application;
        var contract = located.Slice.Events.Single(contract => contract.Id == constraint.Targets[0].EventContract);
        var component = contract.Properties[0] with { Id = SemanticId.Create(SemanticKind.Property, "decimal-owner"), Name = "orderId", Type = SemanticTypeReference.ForPrimitive(type) };
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                // The UUID-valued scenarios do not describe these numeric source identities.
                Specifications = [],
                Events = [.. slice.Events.Select(candidate => primitive && candidate.Id == contract.Id ? candidate with { Properties = [.. candidate.Properties, component] } : candidate)]
            })]
        };
        application = application with
        {
            Concepts = [.. application.Concepts.Select(concept => !primitive && concept.Name == "OrderId" ? concept with { Primitive = type } : concept)],
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application) };
    }
}
