// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal sealed class QueryEndpoint(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located, SemanticKeyedQuery query, string modelType, QueryShape shape)
{
    string Caller
    {
        get
        {
            var name = "authentication";
            while (shape.Arguments.Any(argument => JavaNames.Member(argument.Name) == name)) name = "_" + name;
            return name;
        }
    }

    string MetadataName
    {
        get
        {
            var name = "metadata";
            while (shape.Arguments.Any(argument => JavaNames.Member(argument.Name) == name)) name = "_" + name;
            return name;
        }
    }

    internal static string Route(LocatedSemanticSlice owner, SemanticKeyedQuery definition) =>
        $"/api/{RouteSegment(owner.Module.Name)}/{string.Join('/', owner.Features.Select(feature => RouteSegment(feature.Name)))}/{RouteSegment(definition.Name)}";

    internal void Emit()
    {
        var package = types.Package(located);
        var name = JavaNames.Type(query.Name);
        var code = QueryModelReference.Create(context, package).Declare(name).Declare(name + "Endpoint");
        if (query.Delivery == SemanticQueryDelivery.Live) code.Declare(name + "Streams").Declare(name + "Subscription");
        var modelName = QueryModelReference.Claim(code, package, modelType, name, name + "Endpoint", name + "Streams", name + "Subscription");
        shape.Claim(code);
        var gateway = code.Reference("org.axonframework.messaging.queryhandling.gateway.QueryGateway");
        code.Line($"@{code.Reference("org.springframework.web.bind.annotation.RestController")}({JavaCodeBuilder.StringLiteral(package + "." + name + "Endpoint")})")
            .Block($"public class {name}Endpoint", body =>
            {
                var live = query.Delivery == SemanticQueryDelivery.Live;
                body.Line($"private final {gateway} gateway;");
                if (live) body.Line($"private final {name}Streams streams;");
                body.Block($"public {name}Endpoint({gateway} gateway{(live ? $", {name}Streams streams" : string.Empty)})", constructor =>
                {
                    constructor.Line("this.gateway = gateway;");
                    if (live) constructor.Line("this.streams = streams;");
                });
                var authorization = SecurityAnnotations.For(context, query.Authorization, query.Id);

                // Read the expression from the annotation as produced, before any qualification of its type name is rendered.
                var expression = AxonHttpTestsEndpoints.Expression(authorization);
                if (authorization.Length > 0) authorization = authorization.Replace("@PreAuthorize", "@" + body.Reference("org.springframework.security.access.prepost.PreAuthorize"), StringComparison.Ordinal);
                var route = Route(located, query);
                Register(expression, route);
                if (authorization.Length > 0) body.Line(authorization);
                Snapshot(body, name, route, modelName);
                if (live)
                {
                    if (authorization.Length > 0) body.Line(authorization);
                    Live(body, name, route);
                }
            });
        ViewJavaTypes.Add(context, package, name + "Endpoint", code.Build(), query.Id);
    }

    static string RouteSegment(string name) => string.Concat(name.Select((character, index) =>
        char.IsUpper(character) && index > 0 && char.IsLower(name[index - 1]) ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString())).Replace('_', '-');

    void Snapshot(JavaCodeBuilder code, string name, string route, string modelName)
    {
        code.Line($"@{code.Reference("org.springframework.web.bind.annotation.GetMapping")}({JavaCodeBuilder.StringLiteral(route)})");
        var response = code.Reference("org.springframework.http.ResponseEntity");
        var result = query.Cardinality == SemanticQueryCardinality.Many ? $"{code.Reference("java.util.List")}<{modelName}>" : modelName;
        code.Block($"public {code.Reference("java.util.concurrent.CompletableFuture")}<{response}<{result}>> get({shape.Parameters(code)}{(shape.Arguments.Any() ? ", " : string.Empty)}{code.Reference("org.springframework.security.core.Authentication")} {Caller})", method =>
        {
            method.Line($"var {MetadataName} = {method.Reference("org.axonframework.messaging.core.Metadata")}.emptyInstance();")
                .Block($"if ({Caller} != null && {Caller}.isAuthenticated())", authenticated => authenticated
                    .Line($"{MetadataName} = {MetadataName}.and(\"username\", {Caller}.getName()).and(\"authorities\", {Caller}.getAuthorities().stream().map({authenticated.Reference("org.springframework.security.core.GrantedAuthority")}::getAuthority).collect({authenticated.Reference("java.util.stream.Collectors")}.joining(\",\")));"));
            var mapped = query.Cardinality switch
            {
                SemanticQueryCardinality.Many => $"{response}.ok(result.rows())",
                SemanticQueryCardinality.One => $"{response}.ok(result.rows().getFirst())",
                _ => $"result.rows().isEmpty() ? {response}.notFound().build() : {response}.ok(result.rows().getFirst())"
            };
            method.Line($"return gateway.query(new {name}({shape.Parsed(method)}), {name}.Result.class, {MetadataName}, null).thenApply(result -> {mapped});");
        });
    }

    void Live(JavaCodeBuilder code, string name, string route)
    {
        code.Line($"@{code.Reference("org.springframework.web.bind.annotation.GetMapping")}(value = {JavaCodeBuilder.StringLiteral(route)}, produces = {code.Reference("org.springframework.http.MediaType")}.TEXT_EVENT_STREAM_VALUE)")
            .Block($"public {code.Reference("org.springframework.web.servlet.mvc.method.annotation.SseEmitter")} stream({shape.Parameters(code)}{(shape.Arguments.Any() ? ", " : string.Empty)}{code.Reference("org.springframework.security.core.Authentication")} {Caller})", method =>
            {
                method.Line($"var {MetadataName} = {method.Reference("org.axonframework.messaging.core.Metadata")}.emptyInstance();")
                    .Block($"if ({Caller} != null && {Caller}.isAuthenticated())", authenticated => authenticated
                        .Line($"{MetadataName} = {MetadataName}.and(\"username\", {Caller}.getName()).and(\"authorities\", {Caller}.getAuthorities().stream().map({authenticated.Reference("org.springframework.security.core.GrantedAuthority")}::getAuthority).collect({authenticated.Reference("java.util.stream.Collectors")}.joining(\",\")));"))
                    .Line($"var query = new {name}({shape.Parsed(method)});")
                    .Line($"return streams.open({name}Subscription.values(gateway, query, {MetadataName}));");
            });
    }

    void Register(string? expression, string route)
    {
        var parameters = shape.Arguments.Select(argument => new HttpParameter(argument.Name, !argument.Type.IsOptional, argument.Type)).ToArray();
        var cardinality = query.Cardinality switch
        {
            SemanticQueryCardinality.Many => "many",
            SemanticQueryCardinality.One => "one",
            _ => "zeroOrOne"
        };
        AxonHttpTestsEndpoints.Register(context, new("query", "GET", route, null, expression, parameters, cardinality, query.Id));
        if (query.Delivery == SemanticQueryDelivery.Live)
            AxonHttpTestsEndpoints.Register(context, new("stream", "GET", route, "text/event-stream", expression, parameters, cardinality, query.Id));
    }
}
