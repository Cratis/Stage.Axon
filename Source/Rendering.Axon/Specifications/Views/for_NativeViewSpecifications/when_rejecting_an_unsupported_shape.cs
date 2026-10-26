// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_NativeViewSpecifications;

public class when_rejecting_an_unsupported_shape : given.a_native_view_request
{
    void Because()
    {
        var unsupported = _spec with { ThenReadModels = [.. _spec.ThenReadModels.Select(expected => expected with { Exactly = true })] };
        var application = _request.Model.Application;
        SemanticFeature Replace(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Replace)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Specifications = [.. slice.Specifications.Select(spec => spec.Id == _spec.Id ? unsupported : spec)]
            })]
        };
        _request = _request with
        {
            Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application with
            {
                Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Replace)] })]
            })
        };
        _context = new(_request, new(_request.Profile));
        new AxonViewSpecsEmitter().Emit(_context);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CategoriesViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_disabled_annotation() => _java.ShouldContain("import org.junit.jupiter.api.Disabled;");
    [Fact] void should_emit_a_short_disabled_annotation() => _java.ShouldContain("@Disabled(\"STAGE-AXON-0501\")");
    [Fact] void should_keep_exact_expectations_disabled() => _context.Specifications[_spec.Id].DisabledCode.ShouldEqual("STAGE-AXON-0501");
    [Fact] void should_not_partially_replay_the_event() => _java.ShouldNotContain("projector.on(");
    [Fact] void should_explain_the_unsupported_complete_scenario() => _java.ShouldContain("Unsupported modeled view specification");
    [Fact] void should_not_ignore_missing_source_identity() => Plan(_spec with { GivenEvents = [.. _spec.GivenEvents.Select(given => given with { EventSource = null })] }).ShouldBeNull();
    [Fact] void should_not_accept_mixed_clock_expectations() => Plan(_spec with { WhenClock = "2026-10-26T12:00:00.0000000Z" }).ShouldBeNull();
    [Fact] void should_not_accept_mixed_denial_expectations() => Plan(_spec with { ThenDenied = true }).ShouldBeNull();
    [Fact] void should_not_accept_exact_expectations() => Plan(_spec with { WhenClock = null, ThenReadModels = [.. _spec.ThenReadModels.Select(expected => expected with { Exactly = true })] }).ShouldBeNull();
    [Fact] void should_not_ignore_an_unhandled_given_event() => Plan(_spec with { WhenClock = null, GivenEvents = [.. _spec.GivenEvents.Select(given => given with { EventContract = _spec.Id })] }).ShouldBeNull();
    [Fact] void should_not_ignore_an_unhandled_appended_event() => Plan(_spec with { WhenAppended = new(_spec.Id, []) { EventSource = _spec.GivenEvents[0].EventSource } }).ShouldBeNull();
    [Fact] void should_not_ignore_a_missing_appended_source() => Plan(_spec with { WhenAppended = new(_spec.GivenEvents[0].EventContract, _spec.GivenEvents[0].Values) }).ShouldBeNull();
    [Fact] void should_not_ignore_another_read_model() => Plan(_spec with { WhenClock = null, ThenReadModels = [.. _spec.ThenReadModels.Select(expected => expected with { ReadModel = _spec.Id })] }).ShouldBeNull();

    Action<Java.JavaCodeBuilder>? Plan(SemanticSpecification spec) => new NativeViewSpecifications(_context, new ViewJavaTypes(_request.Model.Application), _located, _located.Slice.ReadModels.Single(model => model.Name == "Categories")).Plan(spec);
}
