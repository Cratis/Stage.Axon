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

    void Establish() => context = new(_request, new AxonDegradation(_request.Profile));

    void Because()
    {
        new AxonProjectionsEmitter().Emit(context);
        new AxonQueriesEmitter().Emit(context);
        var specification = ScopeSelection.Select(_request).SelectMany(owner => owner.Slice.Specifications).Single(candidate => candidate.Name == "IncreasingTheTotal");
        scenario = context.HttpScenarios[specification.Id];
    }

    [Fact] void should_use_the_generated_snapshot_endpoint_route() => scenario.Exchanges.Single().Request.Path.ShouldEqual("/api/views/counters/all-totals");
    [Fact] void should_arrange_the_modeled_given_events_on_the_real_projector() => scenario.Arrange.Any(line => line.Contains("projector.on(new axonviews.views.events.Increased", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_assert_the_modeled_expected_values_from_http_json() => scenario.Exchanges.Single().Response.Assertions.Single().ShouldContain("row.amount(), 8L");
    [Fact] void should_use_the_application_concept_aware_json_mapper() => scenario.Exchanges.Single().Response.Assertions.Single().ShouldContain("application.getBean(tools.jackson.databind.json.JsonMapper.class)");
    [Fact] void should_not_replace_the_primary_view_specification() => context.Specifications[scenario.Id].TestClass.ShouldContain("TotalViewSpecificationsTest");
    [Fact] void should_keep_unsupported_reducer_expectations_out_of_http_coverage() => context.HttpScenarios.Keys.All(id => context.Specifications[id].DisabledCode is null).ShouldBeTrue();
    [Fact] void should_not_invent_an_authorized_caller() => scenario.Caller.Authenticated.ShouldBeFalse();
}
