// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_admitting_reducer_specifications : a_view_request
{
    LocatedSemanticSlice _located;
    SemanticReadModel _model;
    SemanticSpecification _spec;

    void Establish()
    {
        _located = ScopeSelection.Select(_request).Single(located => located.Slice.ReadModels.Any(model => model.Name == "RebuiltTotal"));
        _model = _located.Slice.ReadModels.Single();
        _spec = _located.Slice.Specifications.Single(spec => spec.Name == "InitializeFromFirstEvent");
    }

    [Fact] void should_admit_the_entire_supported_specification() => Disabled(_spec).ShouldBeNull();
    [Fact] void should_reject_exact_assertions() => Disabled(_spec with { ThenReadModels = [_spec.ThenReadModels[0] with { Exactly = true }] }).ShouldEqual("STAGE-AXON-0501");
    [Fact] void should_reject_empty_assertions() => Disabled(_spec with { ThenReadModels = [_spec.ThenReadModels[0] with { Values = [] }] }).ShouldEqual("STAGE-AXON-0501");
    [Fact] void should_reject_foreign_models() => Disabled(_spec with { ThenReadModels = [_spec.ThenReadModels[0] with { ReadModel = _located.Slice.Id }] }).ShouldEqual("STAGE-AXON-0501");
    [Fact] void should_reject_unrenderable_keys() => Disabled(_spec with { ThenReadModels = [_spec.ThenReadModels[0] with { Key = new SemanticArrayValue([]) }] }).ShouldEqual("STAGE-AXON-0501");
    [Fact] void should_reject_denial_expectations() => Disabled(_spec with { ThenDenied = true }).ShouldEqual("STAGE-AXON-0501");

    [Fact]
    void should_reject_an_unresolvable_property_before_assertion_emission()
    {
        var expected = _spec.ThenReadModels[0];
        Disabled(_spec with { ThenReadModels = [expected with { Values = [expected.Values[0] with { TargetProperty = _located.Slice.Id }] }] }).ShouldEqual("STAGE-AXON-0501");
    }

    [Fact]
    void should_disable_the_whole_specification_for_one_unhandled_event()
    {
        var other = ScopeSelection.Select(_request).SelectMany(located => located.Slice.Events).First(@event => !_located.Slice.Reducers.Single().Transitions.Any(transition => transition.EventContract == @event.Id));
        Disabled(_spec with { GivenEvents = [.. _spec.GivenEvents, _spec.GivenEvents[0] with { EventContract = other.Id }] }).ShouldEqual("STAGE-AXON-0306");
    }

    [Fact]
    void should_not_guess_a_key_from_differing_event_sources()
    {
        var given = _spec.GivenEvents[0];
        var expected = _spec.ThenReadModels[0];
        var different = given with { EventSource = given.EventSource! with { Value = new SemanticTextValue("different") } };
        Disabled(_spec with { GivenEvents = [given, different], ThenReadModels = [expected with { Key = new SemanticNullValue() }] }).ShouldEqual("STAGE-AXON-0501");
    }

    [Fact]
    void should_admit_an_append_only_scenario_with_one_text_source()
    {
        var given = _spec.GivenEvents[0];
        Disabled(_spec with { GivenEvents = [], WhenAppended = new(given.EventContract, given.Values) { EventSource = given.EventSource } }).ShouldBeNull();
    }

    [Fact]
    void should_reject_non_text_sources_even_with_an_explicit_key()
    {
        var given = _spec.GivenEvents[0];
        Disabled(_spec with { GivenEvents = [given with { EventSource = given.EventSource! with { Value = new SemanticNumberValue(1) } }] }).ShouldEqual("STAGE-AXON-0501");
    }

    [Fact]
    void should_reject_missing_event_sources() => Disabled(_spec with { GivenEvents = [_spec.GivenEvents[0] with { EventSource = null }] }).ShouldEqual("STAGE-AXON-0501");

    [Fact]
    void should_guard_composite_keys_before_resolving_their_properties()
    {
        var expected = _spec.ThenReadModels[0];
        Disabled(_spec with { ThenReadModels = [expected with { Key = new SemanticCompositeValue([expected.Values[0] with { TargetProperty = _located.Slice.Id }]) }] }).ShouldEqual("STAGE-AXON-0501");
    }

    [Theory]
    [InlineData("array")]
    [InlineData("composite")]
    [InlineData("boolean")]
    void should_reject_predicates_incompatible_with_a_scalar_type(string shape)
    {
        var expected = _spec.ThenReadModels[0];
        var amount = _model.Properties.Single(property => property.Name == "amount");
        SemanticValue value = shape switch
        {
            "array" => new SemanticArrayValue([]),
            "composite" => new SemanticCompositeValue([expected.Values[0]]),
            _ => new SemanticBooleanValue(true)
        };
        Disabled(_spec with { ThenReadModels = [expected, expected with { Values = [new(amount.Id, value)] }] }).ShouldEqual("STAGE-AXON-0501");
    }

    [Fact]
    void should_reject_nested_collection_predicates_before_emitting_any_subset()
    {
        var amount = _model.Properties.Single(property => property.Name == "amount");
        _model = _model with { Properties = [.. _model.Properties.Select(property => property == amount ? property with { Type = property.Type with { IsCollection = true } } : property)] };
        var expected = _spec.ThenReadModels[0] with { Values = [new(amount.Id, new SemanticArrayValue([new SemanticArrayValue([])]))] };
        Disabled(_spec with { ThenReadModels = [expected] }).ShouldEqual("STAGE-AXON-0501");
    }

    [Theory]
    [InlineData("caller")]
    [InlineData("clock")]
    [InlineData("seed")]
    [InlineData("command")]
    [InlineData("append source")]
    void should_reject_the_complete_unsupported_scenario(string shape)
    {
        var given = _spec.GivenEvents[0];
        var unsupported = shape switch
        {
            "caller" => _spec with { GivenCaller = new(false, [], []) },
            "clock" => _spec with { WhenClock = "2026-10-26T12:00:00.0000000Z" },
            "seed" => _spec with { GivenReadModels = _spec.ThenReadModels },
            "command" => _spec with { When = new(_located.Slice.Id, []) },
            _ => _spec with { WhenAppended = new(given.EventContract, given.Values) }
        };
        Disabled(unsupported).ShouldEqual("STAGE-AXON-0501");
    }

    [Theory]
    [InlineData("query")]
    [InlineData("event")]
    [InlineData("error")]
    [InlineData("no events")]
    void should_reject_the_complete_unsupported_outcome(string shape)
    {
        var unsupported = shape switch
        {
            "query" => _spec with { ThenQueries = [new(_located.Slice.Id, new SemanticNullValue(), [])] },
            "event" => _spec with { ThenEvents = [_spec.GivenEvents[0]] },
            "error" => _spec with { ThenErrors = [new("error", "unsupported")] },
            _ => _spec with { ThenNoEvents = true }
        };
        Disabled(unsupported).ShouldEqual("STAGE-AXON-0501");
    }

    string? Disabled(SemanticSpecification spec)
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        ReducerViewSpecifications.Emit(context, new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name)), _located, _model, [spec]);
        if (context.Specifications[spec.Id].DisabledCode is not null)
        {
            var java = Text(context.Artifacts.Single());
            java.ShouldContain("@Disabled(\"" + context.Specifications[spec.Id].DisabledCode);
            java.ShouldNotContain("assertTrue(");
            java.ShouldNotContain("var projector =");
            java.ShouldNotContain("new GenericEventMessage");
        }
        return context.Specifications[spec.Id].DisabledCode;
    }
}
