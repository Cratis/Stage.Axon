// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_selecting_http_row_examples : a_query_collision
{
    [Fact]
    void should_prefer_expected_rows_to_seed_rows()
    {
        ArrangeExamples(seedOnly: false);
        HttpModeledExamples.Row(_context, _model)["amount"].ShouldEqual(8m);
    }

    [Fact]
    void should_use_seed_rows_when_expectations_are_incomplete()
    {
        ArrangeExamples(seedOnly: true);
        HttpModeledExamples.Row(_context, _model)["amount"].ShouldEqual(99m);
    }

    [Fact]
    void should_skip_the_proof_with_a_diagnostic_when_no_example_is_available()
    {
        var application = _request.Model.Application;
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Remove)] })] }) };
        _query = _query with { Cardinality = SemanticQueryCardinality.ZeroOrOne };
        Render();
        AxonHttpCardinalityTests.Emit(_context);
        _context.Artifacts.ShouldNotContain(artifact => artifact.RelativePath.EndsWith("/HttpOptionalQueryContractTest.java", StringComparison.Ordinal));
        _context.Degradation.Warnings.ShouldContain(warning => warning.Code == "STAGE-AXON-HTTP-SPEC" && warning.Artifact == _query.Id);
    }

    void ArrangeExamples(bool seedOnly)
    {
        SemanticFeature Update(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Update)],
            Slices = [.. feature.Slices.Select(slice => slice with { Specifications = [.. slice.Specifications.Where(spec => spec.Name == "IncreasingTheTotal").Select(spec => Seed(spec, seedOnly))] })]
        };
        var application = _request.Model.Application;
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Update)] })] }) };
        Render();
    }

    SemanticSpecification Seed(SemanticSpecification specification, bool seedOnly)
    {
        var expected = specification.ThenReadModels.Single();
        var amount = _model.Properties.Single(property => property.Name == "amount");
        var seed = expected with { Values = [.. expected.Values.Select(value => value.TargetProperty == amount.Id ? value with { Value = new SemanticNumberValue(99) } : value)] };
        return specification with { GivenReadModels = [seed], ThenReadModels = seedOnly ? [expected with { Values = [expected.Values[0]] }] : [expected] };
    }

    static SemanticFeature Remove(SemanticFeature feature) => feature with
    {
        Features = [.. feature.Features.Select(Remove)],
        Slices = [.. feature.Slices.Select(slice => slice with { Specifications = [] })]
    };
}
