// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Specifications.Views.for_AxonViewLiveSpecs;

public class when_emitting_live_view_tests : for_NativeViewSpecifications.given.a_native_view_request
{
    string _sse;

    async Task Establish()
    {
        _request = await Load("axon-samples/views");
        _context = new(_request, new(_request.Profile));
        _located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "LiveTotals"));
    }

    void Because()
    {
        var query = _located.Slice.Queries.Single(query => query.Name == "LiveTotals");
        var model = _located.Slice.ReadModels.Single(model => model.Id == query.ReadModel);
        AxonViewLiveSpecs.Emit(_context, new ViewJavaTypes(_request.Model.Application), _located, query, _located, model);
        _java = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SubscriptionTest.java", StringComparison.Ordinal)));
        _sse = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("SseTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_import_the_spring_test_annotation() => _java.ShouldContain("import org.springframework.boot.test.context.SpringBootTest;");
    [Fact] void should_shorten_the_spring_test_annotation() => _java.ShouldContain("@SpringBootTest(classes = Application.class)");
    [Fact] void should_import_the_native_gateway() => _java.ShouldContain("import org.axonframework.messaging.queryhandling.gateway.QueryGateway;");
    [Fact] void should_shorten_the_native_gateway() => _java.ShouldContain("private QueryGateway gateway;");
    [Fact] void should_import_flow_as_the_nested_type_owner() => _java.ShouldContain("import java.util.concurrent.Flow;");
    [Fact] void should_shorten_the_flow_subscription() => _java.ShouldContain("new AtomicReference<Flow.Subscription>()");
    [Fact] void should_keep_the_reactive_subscription_distinct() => _java.ShouldContain("onSubscribe(Subscription upstream)");
    [Fact] void should_import_the_native_update_emitter() => _java.ShouldContain("import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;");
    [Fact] void should_import_the_sse_update_emitter() => _sse.ShouldContain("import org.axonframework.messaging.queryhandling.QueryUpdateEmitter;");
    [Fact] void should_import_the_sse_mapper() => _sse.ShouldContain("import tools.jackson.databind.json.JsonMapper;");
    [Fact] void should_shorten_the_sse_mapper() => _sse.ShouldContain("var mapper = application.getBean(JsonMapper.class);");
    [Fact] void should_import_the_shutdown_duration() => _sse.ShouldContain("import java.time.Duration;");
    [Fact] void should_shorten_the_shutdown_duration() => _sse.ShouldContain("elapsed.compareTo(Duration.ofSeconds(5)) < 0");
    [Fact] void should_import_the_http_client() => _sse.ShouldContain("import java.net.http.HttpClient;");
    [Fact] void should_shorten_the_http_client() => _sse.ShouldContain("HttpClient.newHttpClient()");
    [Fact] void should_not_import_a_same_package_projector() => _sse.ShouldNotContain("import axonviews.views.counters.totals.TotalProjectionProjector;");
}
