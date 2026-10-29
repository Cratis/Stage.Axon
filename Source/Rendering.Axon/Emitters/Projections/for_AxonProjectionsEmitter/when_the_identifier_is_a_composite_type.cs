// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_the_identifier_is_a_composite_type : given.a_view_request
{
    string _key;
    string _expression;
    string _expected;

    void Because()
    {
        var located = ScopeSelection.Select(_request).Single(owner => owner.Slice.ReadModels.Any(model => model.Name == "Metric"));
        var original = located.Slice.ReadModels.Single(model => model.Name == "Metric");
        var parts = original.Properties.Where(property => property.IsIdentifier).Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, "metric-key-" + property.Name), IsIdentifier = false }).ToArray();
        var composite = new SemanticCompositeType(SemanticId.Create(SemanticKind.CompositeType, "metric-key"), "MetricIdentity", [.. parts]);
        var identifier = new SemanticProperty(SemanticId.Create(SemanticKind.Property, "metric-id"), "identity", new(SemanticTypeReferenceKind.CompositeType, SemanticPrimitiveType.Unknown, composite.Id, false, false), true);
        var model = original with { Properties = [identifier, .. original.Properties.Where(property => !property.IsIdentifier)] };
        var application = _request.Model.Application with { Types = [.. _request.Model.Application.Types, composite] };
        var semantic = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application);
        var context = new AxonEmissionContext(_request with { Model = semantic }, new(_request.Profile));
        var types = new ViewJavaTypes(application, "axonviews");
        ReadModelKeys.Emit(context, types, types.Package(located), model);
        _key = Text(context.Artifacts.Single());
        var assigned = new SemanticProjectionCompositeKey(composite.Id, [
            new(parts[1].Id, new SemanticProjectionLiteral(new SemanticNumberValue(2))),
            new(parts[0].Id, new SemanticProjectionLiteral(new SemanticTextValue("west")))]);
        _expression = new ProjectionValues(context, types, new JavaCodeBuilder(types.Package(located))).EntityKey(assigned, located.Slice.Events[0], model);
        var expected = new SemanticCompositeValue([new(parts[1].Id, new SemanticNumberValue(2)), new(parts[0].Id, new SemanticTextValue("west"))]);
        _expected = ViewExpectedKeys.Expression(expected, model, types, new JavaCodeBuilder(types.Package(located)));
    }

    [Fact] void should_flatten_the_identifier_into_typed_components() => _key.ShouldContain("String region, @Column(name = \"k_month\") Long month");
    [Fact] void should_order_projection_parts_by_the_composite_declaration() => _expression.ShouldEqual("new MetricKey(\"west\", 2L)");
    [Fact] void should_order_expected_parts_by_the_same_declaration() => _expected.ShouldEqual(_expression);
}
