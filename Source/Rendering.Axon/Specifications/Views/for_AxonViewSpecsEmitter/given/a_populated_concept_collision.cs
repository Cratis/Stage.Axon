// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewSpecsEmitter.given;

public class a_populated_concept_collision : for_NativeViewSpecifications.given.a_native_view_request
{
    string? _eventName;

    protected void Prepare(bool native, string conceptName = "ReadModelStore", string? eventName = null)
    {
        _eventName = eventName;
        var application = _request.Model.Application;
        var concept = application.Concepts.Single(concept => concept.Name == "CategoryStatus");
        var given = _spec.GivenEvents.Single();
        var contract = Sdk.ScopeSelection.Locate(application).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == given.EventContract);
        var identity = contract.Properties.Single(property => property.Type.Target == concept.Id);
        _spec = _spec with
        {
            GivenCaller = native ? _spec.GivenCaller : null,
            GivenEvents = [given with { Values = [.. given.Values.Where(value => value.TargetProperty != identity.Id), new(identity.Id, new SemanticTextValue("visible"))] }]
        };
        application = application with
        {
            Concepts = [.. application.Concepts.Select(candidate => candidate.Id == concept.Id ? new SemanticConcept(concept.Id, conceptName, SemanticPrimitiveType.Text, [], []) : candidate)],
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _context = new(_request, new(_request.Profile));
        _located = Sdk.ScopeSelection.Select(_request).Single(slice => slice.Slice.Id == _located.Slice.Id);
    }

    SemanticFeature Feature(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(Feature)],
        Slices = [.. feature.Slices.Select(slice => slice with
        {
            Events = [.. slice.Events.Select(contract => contract.Id == _spec.GivenEvents.Single().EventContract && _eventName is not null ? contract with { Name = _eventName } : contract)],
            Specifications = slice.Id == _located.Slice.Id ? [_spec] : slice.Specifications
        })]
    };
}
