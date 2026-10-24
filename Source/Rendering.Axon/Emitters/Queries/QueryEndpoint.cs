// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal sealed class QueryEndpoint(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticKeyedQuery query, string modelType, QueryShape shape)
{
    internal static string Route(LocatedSemanticSlice owner, SemanticKeyedQuery definition) =>
        $"/api/{RouteSegment(owner.Module.Name)}/{string.Join('/', owner.Features.Select(feature => RouteSegment(feature.Name)))}/{RouteSegment(definition.Name)}";

    internal void Emit()
    {
        var package = types.Package(located);
        var name = JavaNames.Type(query.Name);
        var code = new JavaCodeBuilder(package)
            .Import("org.springframework.web.bind.annotation.RestController").Import("org.springframework.web.bind.annotation.GetMapping")
            .Import("org.springframework.web.bind.annotation.RequestParam").Import("org.springframework.http.ResponseEntity")
            .Import("org.springframework.security.access.prepost.PreAuthorize")
            .Import("org.axonframework.messaging.queryhandling.gateway.QueryGateway")
            .Line($"@RestController({JavaCodeBuilder.StringLiteral(package + "." + name + "Endpoint")})")
            .Block($"public class {name}Endpoint", body =>
            {
                var live = query.Delivery == SemanticQueryDelivery.Live;
                body.Line("private final QueryGateway gateway;");
                if (live) body.Line($"private final {name}Streams streams;");
                body.Block($"public {name}Endpoint(QueryGateway gateway{(live ? $", {name}Streams streams" : string.Empty)})", constructor =>
                {
                    constructor.Line("this.gateway = gateway;");
                    if (live) constructor.Line("this.streams = streams;");
                });
                var authorization = SecurityAnnotations.For(context, query.Authorization, query.Id);
                if (authorization.Length > 0) body.Line(authorization);
                var route = Route(located, query);
                if (query.Delivery == SemanticQueryDelivery.Live) Live(body, name, route);
                else Snapshot(body, name, route);
            });
        ViewJavaTypes.Add(context, package, name + "Endpoint", code.Build(), query.Id);
    }

    static string RouteSegment(string name) => string.Concat(name.Select((character, index) =>
        char.IsUpper(character) && index > 0 && char.IsLower(name[index - 1]) ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString())).Replace('_', '-');

    void Snapshot(JavaCodeBuilder code, string name, string route)
    {
        code.Line($"@GetMapping({JavaCodeBuilder.StringLiteral(route)})");
        var result = query.Cardinality == SemanticQueryCardinality.Many ? $"java.util.List<{modelType}>" : modelType;
        code.Block($"public java.util.concurrent.CompletableFuture<ResponseEntity<{result}>> get({shape.Parameters})", method =>
        {
            var mapped = query.Cardinality == SemanticQueryCardinality.Many
                ? "ResponseEntity.ok(result.rows())"
                : "result.rows().isEmpty() ? ResponseEntity.notFound().build() : ResponseEntity.ok(result.rows().getFirst())";
            method.Line($"return gateway.query(new {name}({shape.Parsed}), {name}.Result.class).thenApply(result -> {mapped});");
        });
    }

    void Live(JavaCodeBuilder code, string name, string route)
    {
        code.Line($"@GetMapping(value = {JavaCodeBuilder.StringLiteral(route)}, produces = org.springframework.http.MediaType.TEXT_EVENT_STREAM_VALUE)")
            .Block($"public org.springframework.web.servlet.mvc.method.annotation.SseEmitter get({shape.Parameters})", method =>
            {
                method.Line($"var query = new {name}({shape.Parsed});")
                    .Line($"return streams.open({name}Subscription.values(gateway, query));");
            });
    }
}
