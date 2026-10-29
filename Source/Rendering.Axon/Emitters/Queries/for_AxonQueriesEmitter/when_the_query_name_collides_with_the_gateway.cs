// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter;

public class when_the_query_name_collides_with_the_gateway : a_view_request
{
    string _endpoint;

    void Because()
    {
        var located = ScopeSelection.Select(_request).Single(slice => slice.Slice.Queries.Any(query => query.Name == "FindTotal"));
        var query = located.Slice.Queries.Single(query => query.Name == "FindTotal") with { Name = "QueryGateway" };
        var model = located.Slice.ReadModels.Single(model => model.Id == query.ReadModel);
        var types = new ViewJavaTypes(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name));
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        new QueryEndpoint(context, types, located, query, types.Package(located) + "." + model.Name, new(query, model, types)).Emit();
        _endpoint = Text(context.Artifacts.Single());
    }

    [Fact] void should_keep_the_application_query_short() => _endpoint.ShouldContain("gateway.query(new QueryGateway(name), QueryGateway.Result.class, metadata, null)");
    [Fact] void should_qualify_the_framework_gateway() => _endpoint.ShouldContain("private final org.axonframework.messaging.queryhandling.gateway.QueryGateway gateway;");
    [Fact] void should_not_import_the_conflicting_gateway() => _endpoint.ShouldNotContain("import org.axonframework.messaging.queryhandling.gateway.QueryGateway;");
    [Fact] void should_still_shorten_unrelated_types() => _endpoint.ShouldContain("CompletableFuture<ResponseEntity<Total>>");
}
