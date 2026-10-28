// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewHttpSpecs;

public class when_registering_modeled_http_queries : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    AxonEmissionContext context;
    AxonHttpScenario scenario;
    string code;

    void Establish() => context = new(_request, new AxonDegradation(_request.Profile));

    void Because()
    {
        new AxonProjectionsEmitter().Emit(context);
        new AxonQueriesEmitter().Emit(context);
        var specification = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Specifications).Single(candidate => candidate.Name == "IncreasingTheTotal");
        scenario = context.HttpScenarios[specification.Id];
        AxonHttpTests.Emit(context);
        code = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ModeledHttpRoundTripTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_use_the_generated_snapshot_endpoint_route() => scenario.Exchanges.Single().Request.Path.ShouldEqual("/api/views/counters/all-totals");
    [Fact] void should_arrange_the_modeled_given_events_on_the_real_projector() => code.ShouldContain("projector.on(new Increased(");
    [Fact] void should_assert_the_modeled_expected_values_from_http_json() => code.ShouldContain("Objects.equals(row.amount(), 8L)");
    [Fact] void should_use_the_application_concept_aware_json_mapper() => code.ShouldContain("application.getBean(JsonMapper.class)");
    [Fact] void should_clear_the_repository_by_its_type() => code.ShouldContain("application.getBean(TotalRepository.class).deleteAll();");
    [Fact] void should_resolve_the_projector_in_the_destination_class() => code.ShouldContain("application.getBean(TotalProjectionProjector.class)");
    [Fact] void should_read_the_short_model_type() => code.ShouldContain("new TypeReference<List<Total>>() {}");
    [Fact] void should_resolve_the_assertions_in_the_destination_class() => code.ShouldContain("Assertions.assertTrue(rows.stream()");

    [Theory]
    [InlineData("axonviews.views.counters.totals.TotalRepository")]
    [InlineData("axonviews.views.counters.totals.TotalProjectionProjector")]
    [InlineData("org.junit.jupiter.api.Assertions")]
    [InlineData("tools.jackson.databind.json.JsonMapper")]
    [InlineData("tools.jackson.core.type.TypeReference")]
    [InlineData("java.util.List")]
    [InlineData("axonviews.views.counters.totals.Total")]
    [InlineData("axonviews.views.events.Increased")]
    [InlineData("java.util.Objects")]
    public void should_import_fragment_types_only_into_the_destination_test(string type)
    {
        code.ShouldContain("import " + type + ";");
        Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RuntimeHttpExchangeTest.java", StringComparison.Ordinal))).ShouldNotContain("import " + type + ";");
    }
    [Fact] void should_not_replace_the_primary_view_specification() => context.Specifications[scenario.Id].TestClass.ShouldContain("TotalViewSpecificationsTest");
    [Fact] void should_keep_unsupported_reducer_expectations_out_of_http_coverage() => context.HttpScenarios.Keys.All(id => context.Specifications[id].DisabledCode is null).ShouldBeTrue();
    [Fact] void should_not_invent_an_authorized_caller() => scenario.Caller.Authenticated.ShouldBeFalse();
}
