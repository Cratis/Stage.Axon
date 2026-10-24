// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Security;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

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
                new QueryEndpoint(context, types, located, query, modelType, shape).Emit();
                if (query.Delivery != SemanticQueryDelivery.Live) continue;
                LiveQuerySubscription.Emit(context, package, query);
                AxonViewLiveSpecs.Emit(context, types, located, query, owner, model);
            }
        }
    }

    static void EmitRecord(AxonEmissionContext context, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var live = query.Delivery == SemanticQueryDelivery.Live;
        var code = new JavaCodeBuilder(package).Block($"public record {name}({shape.Components})", body =>
        {
            if (live)
            {
                body.Block($"public record Result(java.util.List<{modelType}> rows, boolean invalidated)", result => result
                    .Block($"public Result(java.util.List<{modelType}> rows)", constructor => constructor.Line("this(rows, false);"))
                    .Block("public static Result invalidation()", factory => factory.Line("return new Result(java.util.List.of(), true);")));
            }
            else
            {
                body.Line($"public record Result(java.util.List<{modelType}> rows) {{}}");
            }
        });
        ViewJavaTypes.Add(context, package, name, code.Build(), query.Id);
    }

    static void EmitHandler(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var code = new JavaCodeBuilder(package)
            .Import("org.springframework.stereotype.Component").Import("org.springframework.beans.factory.annotation.Qualifier")
            .Import("org.springframework.security.access.prepost.PreAuthorize")
            .Import("org.axonframework.messaging.queryhandling.annotation.QueryHandler").Import(types.BasePackage + ".runtime.ReadModelStore")
            .Line($"@Component({JavaCodeBuilder.StringLiteral(package + "." + name + "Handler")})")
            .Block($"public class {name}Handler", body =>
            {
                body.Line($"private final ReadModelStore<Object, {modelType}> store;")
                    .Block($"public {name}Handler(@Qualifier({JavaCodeBuilder.StringLiteral(modelType)}) ReadModelStore<Object, {modelType}> store)", constructor => constructor.Line("this.store = store;"));
                var authorization = SecurityAnnotations.For(context, query.Authorization, query.Id);
                if (authorization.Length > 0) body.Line(authorization);
                body.Line("@QueryHandler").Block($"public {name}.Result handle({name} query)", method =>
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
}
