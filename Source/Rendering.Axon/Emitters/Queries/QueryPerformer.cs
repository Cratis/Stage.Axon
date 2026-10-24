// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class QueryPerformer
{
    internal static SemanticImplementationRequirement? Requirement(AxonEmissionContext context, SemanticKeyedQuery query) =>
        (context.Request.ImplementationRequirements.IsDefault ? [] : context.Request.ImplementationRequirements).FirstOrDefault(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer && requirement.Source.SemanticId == query.Id);

    internal static bool Required(AxonEmissionContext context, SemanticKeyedQuery query) => Requirement(context, query) is not null;

    internal static IEnumerable<SemanticKeyedQuery> Queries(AxonEmissionContext context, LocatedSemanticSlice located) =>
        (context.Request.ImplementationRequirements.IsDefault ? [] : context.Request.ImplementationRequirements)
            .Where(requirement => requirement.Role == SemanticImplementationRole.QueryPerformer && requirement.QueryShape?.ReadModel is { } id && located.Slice.ReadModels.Any(model => model.Id == id))
            .Select(requirement => new SemanticKeyedQuery(requirement.Source.SemanticId, requirement.Owner.Name, null, requirement.QueryShape!.ReadModel!.Value, null, requirement.QueryShape.Cardinality, requirement.QueryShape.Delivery)
            {
                Authorization = requirement.QueryShape.Authorization
            });

    internal static void Emit(AxonEmissionContext context, string package, SemanticKeyedQuery query, string modelType)
    {
        var name = JavaNames.Type(query.Name);
        const string reason = "Query performer bodies are not translated; implement the generated performer interface as a Spring bean.";
        context.Degradation.Unsupported(AxonRefusalCodes.QueryPerformer, query.Id, reason, "answer the query with its modeled performer", query.Id.ToString());
        var code = new JavaCodeBuilder(package)
            .Import(context.BasePackage + ".runtime.ReadModelStore")
            .DiffersFromScreenplay("the modeled performer is a typed Spring extension point; implementation bodies are not translated.")
            .Block($"public interface {name}Performer", body => body
                .Block($"default java.util.List<{modelType}> perform({name} query, ReadModelStore<Object, {modelType}> store)", method => method
                    .UnsupportedIntent("answer the query with its modeled performer", query.Id.ToString(), AxonRefusalCodes.QueryPerformer)
                    .Line($"throw new UnsupportedOperationException({JavaCodeBuilder.StringLiteral(AxonRefusalCodes.QueryPerformer + ": " + reason)});")));
        context.AddJava(package, name + "Performer", code.Build(), sources: query.Id);
        var configuration = new JavaCodeBuilder(package)
            .Line($"@org.springframework.context.annotation.Configuration({JavaCodeBuilder.StringLiteral(package + "." + name + "PerformerConfiguration")})")
            .Block($"public class {name}PerformerConfiguration", body => body
                .Line($"@org.springframework.context.annotation.Bean({JavaCodeBuilder.StringLiteral(package + "." + name + "Performer")})")
                .Line($"@org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean({name}Performer.class)")
                .Block($"public {name}Performer {JavaNames.Member(name)}Performer()", method => method.Line($"return new {name}Performer() {{}};")));
        context.AddJava(package, name + "PerformerConfiguration", configuration.Build(), sources: query.Id);
    }
}
