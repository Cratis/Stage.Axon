// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
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
            foreach (var query in located.Slice.Queries.Concat(QueryPerformer.Queries(context, located)))
            {
                if (QueryPerformer.Requirement(context, query)?.QueryShape?.Arguments.Any(argument => argument.Type is null) == true)
                {
                    context.Degradation.Unsupported(AxonRefusalCodes.QueryPerformer, query.Id, "The performed query has an unresolved argument type.");
                    continue;
                }

                var owner = ScopeSelection.Locate(application).First(slice => slice.Slice.ReadModels.Any(model => model.Id == query.ReadModel));
                var model = owner.Slice.ReadModels.First(model => model.Id == query.ReadModel);
                var modelType = types.Package(owner) + "." + JavaNames.Type(model.Name);
                var shape = new QueryShape(query, model, types, QueryPerformer.Requirement(context, query));
                var package = types.Package(located);
                var name = JavaNames.Type(query.Name);
                EmitRecord(context, package, query, name, modelType, shape);
                if (QueryPerformer.Required(context, query)) QueryPerformer.Emit(context, package, query, modelType);
                EmitHandler(context, types, package, query, name, modelType, shape);
                new QueryEndpoint(context, types, located, query, modelType, shape).Emit();
                if (query.Delivery != SemanticQueryDelivery.Live) continue;
                LiveQuerySubscription.Emit(context, package, query, modelType);
                LiveQueryStreams.Emit(context, package, query, modelType);
                AxonViewLiveSpecs.Emit(context, types, located, query, owner, model);
            }
        }
    }

    static void EmitRecord(AxonEmissionContext context, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var live = query.Delivery == SemanticQueryDelivery.Live;
        var code = QueryModelReference.Create(context, package).Declare(name).Declare("Result");
        var modelName = QueryModelReference.Claim(code, package, modelType, name, "Result");
        shape.Claim(code);
        var list = code.Reference("java.util.List");
        code.Block($"public record {name}({shape.Components(code)})", body =>
        {
            if (live)
            {
                body.Block($"public record Result({list}<{modelName}> rows, boolean invalidated)", result => result
                    .Block($"public Result({list}<{modelName}> rows)", constructor => constructor.Line("this(rows, false);"))
                    .Block("public static Result invalidation()", factory => factory.Line($"return new Result({list}.of(), true);")));
            }
            else
            {
                body.Line($"public record Result({list}<{modelName}> rows) {{}}");
            }
        });
        ViewJavaTypes.Add(context, package, name, code.Build(), query.Id);
    }

    static void EmitHandler(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticKeyedQuery query, string name, string modelType, QueryShape shape)
    {
        var code = QueryModelReference.Create(context, package).Declare(name).Declare(name + "Handler").Declare(name + "Performer");
        var modelName = QueryModelReference.Claim(code, package, modelType, name, name + "Handler", name + "Performer");
        var store = code.Reference(types.BasePackage + ".runtime.ReadModelStore");
        code.Line($"@{code.Reference("org.springframework.stereotype.Component")}({JavaCodeBuilder.StringLiteral(package + "." + name + "Handler")})")
            .Block($"public class {name}Handler", body =>
            {
                var performed = QueryPerformer.Required(context, query);
                body.Line($"private final {store}<{body.Reference("java.lang.Object")}, {modelName}> store;");
                if (performed) body.Line($"private final {name}Performer performer;");
                body.Block($"public {name}Handler(@{body.Reference("org.springframework.beans.factory.annotation.Qualifier")}({JavaCodeBuilder.StringLiteral(modelType)}) {store}<{body.Reference("java.lang.Object")}, {modelName}> store{(performed ? $", {name}Performer performer" : string.Empty)})", constructor =>
                {
                    constructor.Line("this.store = store;");
                    if (performed) constructor.Line("this.performer = performer;");
                });
                var authorization = SecurityAnnotations.For(context, query.Authorization, query.Id);
                if (authorization.Length > 0) body.Line(authorization.Replace("@PreAuthorize", "@" + body.Reference("org.springframework.security.access.prepost.PreAuthorize"), StringComparison.Ordinal));
                body.Line("@" + body.Reference("org.axonframework.messaging.queryhandling.annotation.QueryHandler")).Block($"public {name}.Result handle({name} query)", method =>
                {
                    var predicate = QueryPerformer.Required(context, query) ? string.Empty : shape.Predicate(method);
                    method.Line(performed
                        ? $"var rows = {method.Reference("java.util.List")}.copyOf(performer.perform(query, store));"
                        : $"var rows = store.all().stream(){(predicate.Length > 0 ? $".filter(row -> {predicate})" : string.Empty)}.toList();");
                    if (query.Cardinality == SemanticQueryCardinality.One)
                        method.Line($"{method.Reference(types.BasePackage + ".runtime.QueryResults")}.exactlyOne(rows);");
                    if (query.Cardinality == SemanticQueryCardinality.ZeroOrOne)
                        method.Line($"{method.Reference(types.BasePackage + ".runtime.QueryResults")}.atMostOne(rows);");
                    method.Line($"return new {name}.Result(rows);");
                });
            });
        ViewJavaTypes.Add(context, package, name + "Handler", code.Build(), query.Id);
    }
}
