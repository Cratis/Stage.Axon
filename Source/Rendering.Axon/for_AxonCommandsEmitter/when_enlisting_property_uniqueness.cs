// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_enlisting_property_uniqueness : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan plan;
    string handler;
    string entity;
    string claimed;
    string released;

    void Because()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var command = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "PlaceOrder");
        var contract = slices.SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == command.Produces[0].EventContract);
        var release = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "MoveOrder").Produces[0].EventContract;
        var property = contract.Properties.Single(property => property.Name == "customer");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [property.Id])], [release], false, "Customer is already claimed");
        var carried = property with { Id = SemanticId.Create(SemanticKind.Property, "released-customer"), Type = property.Type with { IsOptional = true } };
        SemanticEventContract Carry(SemanticEventContract candidate) => candidate.Id == release ? candidate with { Properties = [.. candidate.Properties, carried] } : candidate;
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice with { Events = [.. slice.Events.Select(Carry)], Specifications = [], Constraints = slice.Commands.Any(candidate => candidate.Id == command.Id) ? [constraint] : slice.Constraints })] };
        var app = loaded.Model.Application with { Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, app);
        request = new(model, SemanticExecutionPlan.Compile(model).Plan, request.Profile, new(ArtifactRenderScopeKind.Application, app.Id));
        plan = new AxonArtifactRenderPlanner().Plan(request);
        handler = Source("PlaceOrderHandler.java");
        entity = Source("CustomerUniqueSetValidation.java");
        claimed = Source("OrderPlaced.java");
        released = Source("OrderMoved.java");
    }

    string Source(string name) => Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)).Bytes.AsSpan());

    [Fact] void should_inject_the_set_validation_entity() => handler.ShouldContain("@InjectEntity(idProperty = \"customer\") CustomerUniqueSetValidation customerUnique");
    [Fact] void should_assert_before_appending() => (handler.IndexOf("customerUnique.assertCustomerUnique(", StringComparison.Ordinal) < handler.IndexOf("appender.append(", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_not_use_the_unique_value_store() => handler.ShouldNotContain("uniqueValues");
    [Fact] void should_not_decorate_the_appender() => handler.ShouldNotContain("ModeledConstraints");
    [Fact] void should_declare_the_entity_on_the_constraint_tag() => entity.ShouldContain("@EventSourced(tagKey = EventTags.CUSTOMER_UNIQUE");
    [Fact] void should_throw_the_modeled_message() => entity.ShouldContain("Customer is already claimed");
    [Fact] void should_not_build_criteria_for_case_sensitive_values() => entity.ShouldNotContain("EventCriteriaBuilder");
    [Fact] void should_not_lowercase_the_claim_tag() => claimed.ShouldNotContain("toLowerCase");
    [Fact] void should_tag_the_release_event_too() => released.ShouldContain("@EventTag(key = EventTags.CUSTOMER_UNIQUE)");
    [Fact] void should_not_refuse_supported_uniqueness() => plan.Diagnostics.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0801").ShouldBeFalse();
}
