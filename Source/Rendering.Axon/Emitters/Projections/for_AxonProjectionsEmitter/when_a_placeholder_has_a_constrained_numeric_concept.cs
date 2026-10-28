// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Concepts;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_a_placeholder_has_a_constrained_numeric_concept : given.a_view_request
{
    string _java;
    string _concept;
    string _model;

    void Because()
    {
        static SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice.Reducers.IsEmpty ? slice : slice with { Specifications = [] })]
        };
        var application = _request.Model.Application;
        var original = application.Concepts.Single(concept => concept.Name == "CounterId");
        var quantity = original with
        {
            Name = "Quantity",
            Primitive = SemanticPrimitiveType.WholeNumber,
            Validations = [new(default, SemanticValidationRuleKind.GreaterThan, new SemanticNumberValue(0), null)]
        };
        var model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
        {
            Concepts = [.. application.Concepts.Select(concept => concept.Id == original.Id ? quantity : concept)],
            Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })]
        });
        var request = _request with { Model = model, ImplementationContents = _request.ImplementationContents.Clear() };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        new AxonConceptsEmitter().Emit(context);
        _java = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalStoreConfiguration.java", StringComparison.Ordinal)));
        _concept = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Quantity.java", StringComparison.Ordinal)));
        _model = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotal.java", StringComparison.Ordinal)));
    }

    [Fact] void should_use_a_concept_whose_constructor_rejects_zero() => _concept.ShouldContain("if (!(new BigDecimal(value.toString()).compareTo(new BigDecimal(\"0\")) > 0)) throw new IllegalArgumentException");
    [Fact] void should_put_the_constrained_concept_in_the_read_model() => _model.ShouldContain("public record RebuiltTotal(Quantity id, Long amount)");
    [Fact] void should_not_construct_a_substitute_row() => _java.ShouldNotContain("new RebuiltTotal(");
    [Fact] void should_not_invoke_a_concept_constructor() => _java.ShouldNotContain("new Quantity(");
    [Fact] void should_assert_unavailable_reads() => _java.ShouldContain("throw new IllegalStateException");
    [Fact] void should_not_assert_successful_reduction() => _java.ShouldNotContain("reducer.reduce(");
}
