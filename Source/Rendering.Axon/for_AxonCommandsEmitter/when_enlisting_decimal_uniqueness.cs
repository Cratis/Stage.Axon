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

public class when_enlisting_decimal_uniqueness : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    ArtifactRenderPlan Plan(bool primitive)
    {
        var slices = ScopeSelection.Select(request).ToArray();
        var command = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "PlaceOrder");
        var contract = slices.SelectMany(slice => slice.Slice.Events).Single(contract => contract.Id == command.Produces[0].EventContract);
        var release = slices.SelectMany(slice => slice.Slice.Commands).Single(command => command.Name == "MoveOrder").Produces[0].EventContract;
        var property = contract.Properties.Single(property => property.Name == "customer");
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [new(contract.Id, [property.Id])], [release], false, "Customer is already claimed");
        var carried = property with { Id = SemanticId.Create(SemanticKind.Property, "released-customer"), Type = property.Type with { IsOptional = true } };
        SemanticProperty Retype(SemanticProperty candidate) => candidate;
        SemanticEventContract Carry(SemanticEventContract candidate) => (candidate.Id == release ? candidate with { Properties = [.. candidate.Properties, carried] } : candidate) with { };
        SemanticEventContract Change(SemanticEventContract candidate) => Carry(candidate) with { Properties = [.. Carry(candidate).Properties.Select(Retype)] };
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice with { Events = [.. slice.Events.Select(Change)], Specifications = [], Constraints = slice.Commands.Any(candidate => candidate.Id == command.Id) ? [constraint] : slice.Constraints })] };
        var concept = property.Type.Target;
        var app = loaded.Model.Application with
        {
            Concepts = [.. loaded.Model.Application.Concepts.Select(candidate => !primitive && candidate.Id == concept ? candidate with { Primitive = SemanticPrimitiveType.DecimalNumber } : candidate)],
            Modules = [.. loaded.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        };
        var model = ExecutableSemanticModel.Create(loaded.Model.LanguageVersion, loaded.Model.SemanticVersion, app);
        request = new(model, SemanticExecutionPlan.Compile(model).Plan, request.Profile, new(ArtifactRenderScopeKind.Application, app.Id));
        return new AxonArtifactRenderPlanner().Plan(request);
    }

    static string Source(ArtifactRenderPlan plan, string name) => Encoding.UTF8.GetString(plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name, StringComparison.Ordinal)).Bytes.AsSpan());

    [Theory]
    [InlineData(false, "customer.value().stripTrailingZeros().toPlainString()")]
    public void should_tag_the_canonical_decimal_on_claim_and_guard_the_optional_release(bool primitive, string expression)
    {
        var plan = Plan(primitive);
        var claimed = Source(plan, "OrderPlaced.java");
        claimed.ShouldContain("public String customerUniqueKey()");
        claimed.ShouldContain("return " + expression + ";");
        claimed.ShouldNotContain("customer == null ? null");
        var released = Source(plan, "OrderMoved.java");
        released.ShouldContain("public String customerUniqueKey()");
        released.ShouldContain("return customer == null ? null : " + expression + ";");
    }

    [Theory]
    [InlineData(false, "id.value().stripTrailingZeros().toPlainString()")]
    public void should_build_entity_criteria_from_the_same_canonical_decimal(bool primitive, string expression)
    {
        var entity = Source(Plan(primitive), "CustomerUniqueSetValidation.java");
        entity.ShouldContain("@EventCriteriaBuilder");
        entity.ShouldContain("Tag.of(EventTags.CUSTOMER_UNIQUE, " + expression + ")");
    }

    [Fact]
    void should_canonicalise_a_decimal_primitive_the_same_way()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var constraint = new SemanticConstraint("CustomerUnique", SemanticConstraintKind.UniquePropertyValue, SemanticConstraintScope.EventSequence, [], [], false, null);
        Emitters.Constraints.ConstraintCoverage.Normalized(context, constraint, SemanticTypeReference.ForPrimitive(SemanticPrimitiveType.DecimalNumber), "id").ShouldEqual("id.stripTrailingZeros().toPlainString()");
    }
}
