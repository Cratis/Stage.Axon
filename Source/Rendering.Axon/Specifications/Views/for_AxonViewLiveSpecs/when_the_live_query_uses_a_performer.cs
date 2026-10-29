// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewLiveSpecs;

public class when_the_live_query_uses_a_performer : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    string _java;

    void Because()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "LiveTotals"));
        var query = QueryPerformer.Queries(context, located).Single(query => query.Name == "RankedTotals") with { Delivery = SemanticQueryDelivery.Live };
        var model = located.Slice.ReadModels.Single(model => model.Id == query.ReadModel);
        AxonViewLiveSpecs.Emit(context, new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name)), located, query, located, model);
        _java = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SubscriptionTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_expect_a_refresh_marker() => _java.ShouldContain("assertTrue(payload.get().invalidated())");
    [Fact] void should_not_treat_the_marker_as_handler_rows() => _java.ShouldNotContain("assertEquals(fresh, payload.get()");
    [Fact] void should_compare_the_delivered_rows_with_the_handler() => _java.ShouldContain("assertEquals(fresh, values.getLast())");
}
