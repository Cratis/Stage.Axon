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

public class when_a_command_binds_one_constraint_from_two_properties : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string handler;

    void Because()
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var command = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "PlaceOrder");
        var contract = slices.SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == command.Produces[0].EventContract);
        var property = contract.Properties.Single(property => property.Name == "customer");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [property.Id])], [], false, "Customer is already claimed");
        var first = command.Properties.Single(candidate => candidate.Name == "customer");
        var second = first with { Id = SemanticId.Create(SemanticKind.Property, "second-alias"), Name = "secondAlias" };
        var produced = command.Produces[0];
        var again = produced with { Mappings = [.. produced.Mappings.Select(mapping => mapping.TargetProperty == property.Id && mapping.Source is SemanticResolvedExpression expression ? mapping with { Source = expression with { Target = second.Id } } : mapping)] };
        var changed = command with { Properties = [.. command.Properties, second], Produces = [produced, again] };
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice with { Specifications = [], Commands = [.. slice.Commands.Select(candidate => candidate.Id == command.Id ? changed : candidate)], Constraints = slice.Commands.Any(candidate => candidate.Id == command.Id) ? [constraint] : slice.Constraints })] };
        var app = loaded.Model.Application with { Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, app);
        request = new(model, SemanticExecutionPlan.Compile(model).Plan, request.Profile, new(ArtifactRenderScopeKind.Application, app.Id));
        var plan = new AxonArtifactRenderPlanner().Plan(request);
        handler = Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/PlaceOrderHandler.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_inject_one_entity_per_bound_property_first() => handler.ShouldContain("@InjectEntity(idProperty = \"customer\") CustomerUniqueSetValidation customerUniqueCustomer");
    [Fact] void should_inject_one_entity_per_bound_property_second() => handler.ShouldContain("@InjectEntity(idProperty = \"secondAlias\") CustomerUniqueSetValidation customerUniqueSecondAlias");
    [Fact] void should_assert_the_first_production_against_its_entity() => handler.ShouldContain("customerUniqueCustomer.assertCustomerUnique(");
    [Fact] void should_assert_the_second_production_against_its_entity() => handler.ShouldContain("customerUniqueSecondAlias.assertCustomerUnique(");
}
