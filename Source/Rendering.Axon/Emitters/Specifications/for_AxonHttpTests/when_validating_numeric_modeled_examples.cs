// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_validating_numeric_modeled_examples : a_view_request
{
    const string Model = """
        domain Stage.NumericProof

        concept OrderId : Uuid

        concept Quantity : Int

        concept Price : Decimal

        type Line
          quantity Quantity
          price Price

        module Orders
          feature Lines
            slice StateChange RecordLine
              command RecordOrderLine
                orderId OrderId identifier
                quantity Quantity
                line Line

                produces LineRecorded
                  for orderId
                  orderId = orderId
                  quantity = quantity
                  line = line

            slice StateView Totals
              event LineRecorded
                orderId OrderId
                quantity Quantity
                line Line

              readmodel OrderLines
                orderId OrderId
                quantity Quantity
                line Line

              query LineByOrder => OrderLines optional
                by orderId OrderId

              projection OrderLinesProjection => OrderLines
                from LineRecorded
                  key orderId
                  orderId = orderId
                  quantity = quantity
                  line = line

              specification OutOfRangeFirst
                given LineRecorded
                  for "00000000-0000-0000-0000-000000000001"
                  orderId = "00000000-0000-0000-0000-000000000001"
                  quantity = 0
                  line
                    quantity = 0
                    price = 0.49
                then readmodel OrderLines
                  orderId = "00000000-0000-0000-0000-000000000001"
                  quantity = 0
                  line
                    quantity = 0
                    price = 0.49

              specification InRange
                given LineRecorded
                  for "00000000-0000-0000-0000-000000000002"
                  orderId = "00000000-0000-0000-0000-000000000002"
                  quantity = 5
                  line
                    quantity = 10
                    price = 9.99
                then readmodel OrderLines
                  orderId = "00000000-0000-0000-0000-000000000002"
                  quantity = 5
                  line
                    quantity = 10
                    price = 9.99
        """;

    AxonEmissionContext _context;
    SemanticApplication _application;
    SemanticReadModel _model;
    SemanticTypeReference _quantity;
    SemanticTypeReference _price;
    SemanticTypeReference _line;

    async Task Establish()
    {
        var directory = Directory.CreateTempSubdirectory("numeric-examples");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "model.play"), Model);
            var loaded = await SemanticModelLoader.LoadFromPathAsync(directory.FullName, null, "NumericExamples");
            _request = new(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        }
        finally
        {
            directory.Delete(true);
        }
        _application = _request.Model.Application;
        _model = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.ReadModels).Single(model => model.Name == "OrderLines");
        _quantity = _model.Properties.Single(property => property.Name == "quantity").Type;
        _line = _model.Properties.Single(property => property.Name == "line").Type;
        _price = _application.Types.Single(type => type.Id == _line.Target).Properties.Single(property => property.Name == "price").Type;
        Rules(_quantity, Rule(SemanticValidationRuleKind.Minimum, 1), Rule(SemanticValidationRuleKind.Maximum, 10));
        Rules(_price, Rule(SemanticValidationRuleKind.Minimum, 0.5m), Rule(SemanticValidationRuleKind.Maximum, 9.99m));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("5", true)]
    [InlineData("10", true)]
    [InlineData("0", false)]
    [InlineData("11", false)]
    [InlineData("5.5", false)]
    void should_prove_whole_number_bounds(string value, bool valid) => Accepts(_quantity, Parse(value)).ShouldEqual(valid);

    [Theory]
    [InlineData("0.5", true)]
    [InlineData("9.99", true)]
    [InlineData("0.49", false)]
    [InlineData("9.991", false)]
    void should_prove_decimal_number_bounds(string value, bool valid) => Accepts(_price, Parse(value)).ShouldEqual(valid);

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    void should_prove_whole_number_equality(int value, bool valid)
    {
        Rules(_quantity, Rule(SemanticValidationRuleKind.Equal, 5));
        Accepts(_quantity, value).ShouldEqual(valid);
    }

    [Fact]
    void should_compare_decimal_equality_without_regard_to_scale()
    {
        Rules(_price, Rule(SemanticValidationRuleKind.Equal, 1.50m));
        Accepts(_price, 1.5m).ShouldBeTrue();
    }

    [Fact]
    void should_reject_a_whole_number_beyond_the_long_range()
    {
        Rules(_quantity, Rule(SemanticValidationRuleKind.Minimum, 1));
        Accepts(_quantity, long.MaxValue + 1m).ShouldBeFalse();
    }

    [Fact]
    void should_skip_a_rule_that_is_not_an_error()
    {
        Rules(_quantity, Rule(SemanticValidationRuleKind.Minimum, 1) with { Severity = SemanticValidationSeverity.Warning });
        Accepts(_quantity, 5).ShouldBeFalse();
    }

    [Theory]
    [InlineData(SemanticValidationRuleKind.GreaterThan)]
    [InlineData(SemanticValidationRuleKind.NotEqual)]
    [InlineData(SemanticValidationRuleKind.LessThanOrEqual)]
    void should_skip_unsupported_numeric_rules(SemanticValidationRuleKind kind)
    {
        Rules(_quantity, Rule(kind, 1));
        Accepts(_quantity, 5).ShouldBeFalse();
    }

    [Fact] void should_accept_a_valid_nested_number() => HttpModeledExamples.Literal(_context, _line, Line(10, 9.99m), out _).ShouldBeTrue();
    [Fact] void should_reject_an_out_of_range_nested_number() => HttpModeledExamples.Literal(_context, _line, Line(5, 10m), out _).ShouldBeFalse();
    [Fact] void should_accept_a_valid_number_collection() => HttpModeledExamples.Literal(_context, _quantity with { IsCollection = true }, Numbers(1, 10), out _).ShouldBeTrue();
    [Fact] void should_reject_a_collection_with_an_out_of_range_number() => HttpModeledExamples.Literal(_context, _quantity with { IsCollection = true }, Numbers(5, 0), out _).ShouldBeFalse();
    [Fact] void should_select_the_first_valid_row_after_an_out_of_range_row() => HttpModeledExamples.Row(_context, _model)["quantity"].ShouldEqual(5m);

    [Fact]
    void should_not_select_a_row_when_every_number_is_out_of_range()
    {
        Rules(_quantity, Rule(SemanticValidationRuleKind.Maximum, 4));
        HttpModeledExamples.Row(_context, _model).ShouldBeNull();
    }

    [Fact]
    void should_emit_the_optional_query_contract_test_from_the_valid_row()
    {
        AxonHttpCardinalityTests.Emit(_context);
        _context.Artifacts.ShouldContain(artifact => artifact.RelativePath.EndsWith("/HttpOptionalQueryContractTest.java", StringComparison.Ordinal));
    }

    static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    static SemanticValidationRule Rule(SemanticValidationRuleKind kind, decimal operand) => new(default, kind, new SemanticNumberValue(operand), null);

    static SemanticArrayValue Numbers(params decimal[] values) => new([.. values.Select(value => (SemanticValue)new SemanticNumberValue(value))]);

    bool Accepts(SemanticTypeReference type, decimal value) => HttpModeledExamples.Literal(_context, type, new SemanticNumberValue(value), out _);

    SemanticCompositeValue Line(decimal quantity, decimal price) =>
        new([.. _application.Types.Single(type => type.Id == _line.Target).Properties.Select(property => new SemanticPropertyValue(property.Id, new SemanticNumberValue(property.Name == "quantity" ? quantity : price)))]);

    void Rules(SemanticTypeReference type, params SemanticValidationRule[] rules)
    {
        _application = _application with { Concepts = [.. _application.Concepts.Select(concept => concept.Id == type.Target ? concept with { Validations = [.. rules] } : concept)] };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, _application) };
        _context = new(_request, new(_request.Profile));
    }
}
