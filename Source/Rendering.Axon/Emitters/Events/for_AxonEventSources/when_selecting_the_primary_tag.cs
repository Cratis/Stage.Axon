// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventSources;

public class when_selecting_the_primary_tag : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    SemanticEventContract contract;
    SemanticProperty identifier;

    void Establish()
    {
        context = new(request, new AxonDegradation(request.Profile));
        var slice = ScopeSelection.Locate(loaded.Model.Application).First(slice => slice.Slice.Commands.Any(command => command.Name == "PlaceOrder"));
        contract = slice.Slice.Events.Single(@event => @event.Name == "OrderPlaced");
        identifier = slice.Slice.Commands.Single(command => command.Name == "PlaceOrder").Properties.Single(property => property.IsIdentifier);
    }

    [Fact]
    void should_share_the_event_source_concept_name_and_type_with_other_emitters()
    {
        var primary = AxonEventSources.PrimaryTag(context, contract);
        primary.ComponentName.ShouldEqual("orderId");
        primary.TagKey.ShouldEqual("orderId");
        primary.JavaType.ShouldEqual(context.BasePackage + ".concepts.OrderId");
    }

    [Fact]
    void should_add_the_missing_primary_component()
    {
        var code = AxonEventSources.Record(context, "example.events", contract, "");
        code.ShouldContain("@org.axonframework.eventsourcing.annotation.EventTag(key = \"orderId\") " + context.BasePackage + ".concepts.OrderId orderId");
        TagCount(code).ShouldEqual(1);
    }

    [Fact]
    void should_reuse_a_matching_modeled_component_without_duplication()
    {
        var modeled = new SemanticEventContract(contract.Id, contract.ContractId, contract.Revision, contract.Name, [identifier, .. contract.Properties]);
        var code = AxonEventSources.Record(context, "example.events", modeled, "");
        TagCount(code).ShouldEqual(1);
        (code.Split("OrderId orderId", StringSplitOptions.None).Length - 1).ShouldEqual(1);
    }

    [Fact]
    void should_remove_extra_tags_and_explain_the_difference()
    {
        var secondary = new SemanticProperty(contract.Properties[0].Id, "secondary", identifier.Type, true);
        var modeled = new SemanticEventContract(contract.Id, contract.ContractId, contract.Revision, contract.Name, [identifier, secondary]) { Tags = ["orderId", "secondary", "tenant"] };
        var code = AxonEventSources.Record(context, "example.events", modeled, "");
        TagCount(code).ShouldEqual(1);
        code.ShouldContain("// Differs from Screenplay:");
        code.ShouldContain("// Screenplay wanted to tag the event with additional modeled identities");
        context.Degradation.UnsupportedConstructs.Single().Code.ShouldEqual("STAGE-AXON-EVENT-TAGS");
    }

    [Fact]
    void should_fall_back_to_the_runtime_identity_when_no_concept_is_modeled()
    {
        var unknown = new SemanticEventContract(loaded.Model.Application.Id, contract.ContractId, contract.Revision, "Unrouted", []);
        var primary = AxonEventSources.PrimaryTag(context, unknown);
        primary.ComponentName.ShouldEqual("eventSourceId");
        primary.TagKey.ShouldEqual("eventSourceId");
        primary.JavaType.ShouldEqual(context.BasePackage + ".runtime.EventSourceId");
    }

    static int TagCount(string code) => code.Split("@org.axonframework.eventsourcing.annotation.EventTag", StringSplitOptions.None).Length - 1;
}
