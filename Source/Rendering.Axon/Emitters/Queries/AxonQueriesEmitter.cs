// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal sealed class AxonQueriesEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var application = context.Request.Model.Application;
        var types = new ViewJavaTypes(application);
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            foreach (var query in located.Slice.Queries)
            {
                var owner = ScopeSelection.Locate(application).First(slice => slice.Slice.ReadModels.Any(model => model.Id == query.ReadModel));
                var model = owner.Slice.ReadModels.First(model => model.Id == query.ReadModel);
                var modelType = types.Package(owner) + "." + JavaNames.Type(model.Name);
                var shape = new QueryShape(query, model, types);
                var package = types.Package(located);
                var name = JavaNames.Type(query.Name);
                EmitRecord(context, package, query, name, modelType, shape);
                EmitHandler(context, types, package, query, name, modelType, shape);
                EmitEndpoint(context, located, package, query, name, modelType, shape);
            }
        }
    }

    static string RouteSegment(string name) => string.Concat(name.Select((character, index) =>
        char.IsUpper(character) && index > 0 && char.IsLower(name[index - 1]) ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString())).Replace('_', '-');

    static void EmitRecord(AxonEmissionContext context, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var code = new JavaCodeBuilder(package).Block($"public record {name}({shape.Components})", body =>
            body.Line($"public record Result(java.util.List<{modelType}> rows) {{}}"));
        ViewJavaTypes.Add(context, package, name, code.Build(), query.Id);
    }

    static void EmitHandler(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var code = new JavaCodeBuilder(package)
            .Import("org.springframework.stereotype.Component").Import("org.springframework.beans.factory.annotation.Qualifier")
            .Import("org.axonframework.messaging.queryhandling.annotation.QueryHandler").Import(types.BasePackage + ".runtime.ReadModelStore")
            .Line($"@Component({JavaCodeBuilder.StringLiteral(package + "." + name + "Handler")})")
            .Block($"public class {name}Handler", body =>
            {
                body.Line($"private final ReadModelStore<Object, {modelType}> store;")
                    .Block($"public {name}Handler(@Qualifier({JavaCodeBuilder.StringLiteral(modelType)}) ReadModelStore<Object, {modelType}> store)", constructor => constructor.Line("this.store = store;"))
                    .Line("@QueryHandler").Block($"public {name}.Result handle({name} query)", method =>
                    {
                        var predicate = shape.Predicate;
                        method.Line($"var rows = store.all().stream(){(predicate.Length > 0 ? $".filter(row -> {predicate})" : string.Empty)}.toList();");
                        if (query.Cardinality == SemanticQueryCardinality.One)
                            method.Line("if (rows.size() != 1) throw new IllegalStateException(\"Expected exactly one query result\");");
                        if (query.Cardinality == SemanticQueryCardinality.ZeroOrOne)
                            method.Line("if (rows.size() > 1) throw new IllegalStateException(\"Expected at most one query result\");");
                        method.Line($"return new {name}.Result(rows);");
                    });
            });
        ViewJavaTypes.Add(context, package, name + "Handler", code.Build(), query.Id);
    }

    static void EmitEndpoint(AxonEmissionContext context, LocatedSemanticSlice located, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var code = new JavaCodeBuilder(package)
            .Import("org.springframework.web.bind.annotation.RestController").Import("org.springframework.web.bind.annotation.GetMapping")
            .Import("org.springframework.web.bind.annotation.RequestParam").Import("org.springframework.http.ResponseEntity")
            .Import("org.axonframework.messaging.queryhandling.gateway.QueryGateway")
            .Line($"@RestController({JavaCodeBuilder.StringLiteral(package + "." + name + "Endpoint")})");
        code.Block($"public class {name}Endpoint", body =>
        {
            body.Line("private final QueryGateway gateway;")
                .Block($"public {name}Endpoint(QueryGateway gateway)", constructor => constructor.Line("this.gateway = gateway;"));
            if (query.Authorization is not null)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.QueryAuthorization, query.Id, "Authorize this query with the modeled caller policy.");
                body.UnsupportedIntent("authorize this query", query.Id.ToString(), AxonRefusalCodes.QueryAuthorization)
                    .Line("@org.springframework.security.access.prepost.PreAuthorize(\"denyAll()\")");
            }
            if (query.Delivery == SemanticQueryDelivery.Live)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.ObservableQuery, query.Id, "Deliver live query updates.");
                body.DiffersFromScreenplay("this endpoint currently returns a snapshot rather than live query updates.");
            }
            var route = $"/api/{RouteSegment(located.Module.Name)}/{string.Join('/', located.Features.Select(feature => RouteSegment(feature.Name)))}/{RouteSegment(query.Name)}";
            body.Line($"@GetMapping({JavaCodeBuilder.StringLiteral(route)})");
            var result = query.Cardinality == SemanticQueryCardinality.Many ? $"java.util.List<{modelType}>" : modelType;
            body.Block($"public java.util.concurrent.CompletableFuture<ResponseEntity<{result}>> get({shape.Parameters})", method =>
            {
                var mapped = query.Cardinality == SemanticQueryCardinality.Many
                    ? "ResponseEntity.ok(result.rows())"
                    : "result.rows().isEmpty() ? ResponseEntity.notFound().build() : ResponseEntity.ok(result.rows().getFirst())";
                method.Line($"return gateway.query(new {name}({shape.Parsed}), {name}.Result.class).thenApply(result -> {mapped});");
            });
        });
        ViewJavaTypes.Add(context, package, name + "Endpoint", code.Build(), query.Id);
    }
}
