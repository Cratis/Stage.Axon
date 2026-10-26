// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewSpecsEmitter.given;

public class a_populated_composite_collision : a_populated_concept_collision
{
    SemanticId _scalar;
    SemanticTypeReference _reference;

    protected void PrepareComposite(bool collection = false)
    {
        Prepare(false, "StatusValue");
        var application = _request.Model.Application;
        _scalar = application.Concepts.Single(concept => concept.Name == "StatusValue").Id;
        var contract = Sdk.ScopeSelection.Locate(application).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == _spec.GivenEvents.Single().EventContract);
        var status = contract.Properties.Single(property => property.Type.Target == _scalar);
        var component = status with { Id = SemanticId.Create(SemanticKind.Property, "nested-status"), Name = "value" };
        var composite = new SemanticCompositeType(SemanticId.Create(SemanticKind.CompositeType, "store-collision"), "ReadModelStore", [component]);
        _reference = new(SemanticTypeReferenceKind.CompositeType, SemanticPrimitiveType.Unknown, composite.Id, false, false) { IsCollection = collection };
        SemanticValue value = new SemanticCompositeValue([new(component.Id, new SemanticTextValue("visible"))]);
        if (collection) value = new SemanticArrayValue([value]);
        _spec = _spec with { GivenEvents = [.. _spec.GivenEvents.Select(given => given with { Values = [.. given.Values.Select(entry => entry.TargetProperty == status.Id ? entry with { Value = value } : entry)] })] };
        application = application with { Types = application.Types.Add(composite), Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _context = new(_request, new(_request.Profile));
    }

    SemanticFeature Feature(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(Feature)],
        Slices = [.. feature.Slices.Select(slice => slice with
        {
            Commands = [.. slice.Commands.Select(command => command with { Properties = [.. command.Properties.Select(Property)] })],
            Events = [.. slice.Events.Select(contract => contract with { Properties = [.. contract.Properties.Select(Property)] })],
            ReadModels = [.. slice.ReadModels.Select(model => model with { Properties = [.. model.Properties.Select(Property)] })],
            Specifications = slice.Id == _located.Slice.Id ? [_spec] : []
        })]
    };

    SemanticProperty Property(SemanticProperty property) => property.Type.Target == _scalar ? property with { Type = _reference } : property;
}
