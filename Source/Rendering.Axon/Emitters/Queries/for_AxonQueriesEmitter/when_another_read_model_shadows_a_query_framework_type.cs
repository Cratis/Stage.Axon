// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_another_read_model_shadows_a_query_framework_type : given.a_query_collision
{
    [Theory]
    [InlineData("Object", "LiveTotalsStreams", "Flow.Publisher<java.lang.Object>")]
    [InlineData("Object", "LiveTotalsSubscription", "Flow.Publisher<java.lang.Object>")]
    [InlineData("Object", "FindTotalHandler", "ReadModelStore<java.lang.Object, Total>")]
    [InlineData("Object", "RankedTotalsPerformer", "ReadModelStore<java.lang.Object, Total>")]
    [InlineData("Override", "LiveTotalsSubscription", "@java.lang.Override")]
    [InlineData("Throwable", "LiveTotalsSubscription", "onError(java.lang.Throwable failure)")]
    [InlineData("String", "FindTotalEndpoint", "required = true) java.lang.String name")]
    [InlineData("QueryResults", "FindTotalHandler", "io.cratis.stage.axon.runtime.QueryResults.atMostOne(rows);")]
    [InlineData("Flow", "LiveTotalsStreams", "java.util.concurrent.Flow.Publisher<Object>")]
    [InlineData("List", "FindTotal", "java.util.List<Total>")]
    [InlineData("QueryGateway", "FindTotalEndpoint", "org.axonframework.messaging.queryhandling.gateway.QueryGateway gateway")]
    [InlineData("ResponseEntity", "FindTotalEndpoint", "org.springframework.http.ResponseEntity<Total>")]
    [InlineData("ReadModelStore", "RankedTotalsPerformer", "axonviews.runtime.ReadModelStore<Object, Total>")]
    [InlineData("Component", "FindTotalHandler", "@org.springframework.stereotype.Component(")]
    [InlineData("RequestParam", "FindTotalEndpoint", "@org.springframework.web.bind.annotation.RequestParam(")]
    [InlineData("Bean", "RankedTotalsPerformerConfiguration", "@org.springframework.context.annotation.Bean(")]
    void should_reserve_the_sibling_without_renaming_the_queried_model(string sibling, string file, string expected)
    {
        _additionalModels = [_model with
        {
            Id = SemanticId.Create(SemanticKind.ReadModel, "sibling-" + sibling),
            Name = sibling,
            Properties = [.. _model.Properties.Select(property => property with { Id = SemanticId.Create(SemanticKind.Property, sibling + "." + property.Name) })]
        }];
        Render();
        Code(file).ShouldContain(expected);
        Code("FindTotal").ShouldContain("List<Total>");
    }
}
