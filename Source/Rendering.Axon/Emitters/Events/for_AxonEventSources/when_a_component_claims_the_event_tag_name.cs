// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventSources;

public class when_a_component_claims_the_event_tag_name : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _java;

    void Because()
    {
        var application = request.Model.Application;
        var concept = new SemanticConcept(SemanticId.Create(SemanticKind.Concept, "EventTag"), "EventTag", SemanticPrimitiveType.Text, [], []);
        application = application with { Concepts = application.Concepts.Add(concept) };
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application);
        var context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        var located = ScopeSelection.Locate(application).First(slice => slice.Slice.Commands.Any(command => command.Name == "PlaceOrder"));
        var contract = located.Slice.Events.Single(contract => contract.Name == "OrderPlaced");
        var identifier = located.Slice.Commands.Single(command => command.Name == "PlaceOrder").Properties.Single(property => property.IsIdentifier);
        var component = identifier with
        {
            Id = SemanticId.Create(SemanticKind.Property, "event-tag-component"),
            Name = "label",
            IsIdentifier = false,
            Type = new(SemanticTypeReferenceKind.Concept, SemanticPrimitiveType.Unknown, concept.Id, false, false)
        };
        _java = AxonEventSources.Record(context, "example.events", contract with { Properties = [identifier, component] }, "");
    }

    [Fact] void should_import_the_component_concept() => _java.ShouldContain("import sample.concepts.EventTag;");
    [Fact] void should_keep_the_component_short() => _java.ShouldContain("EventTag label");
    [Fact] void should_qualify_the_primary_tag_annotation() => _java.ShouldContain("@org.axonframework.eventsourcing.annotation.EventTag(");
    [Fact] void should_not_import_the_primary_tag_annotation() => _java.ShouldNotContain("import org.axonframework.eventsourcing.annotation.EventTag;");
}
