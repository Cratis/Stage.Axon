// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

internal static class QueryAvailability
{
    internal static string? Reason(AxonEmissionContext context, SemanticKeyedQuery query)
    {
        var availability = ReducerModelAvailability.For(context);
        if (QueryPerformer.Required(context, query) && availability.Any)
            return "All opaque Java query performers are refused while any read model is quarantined by an unavailable reducer. Performer dependencies are not modeled; this includes unrelated performers and cannot be narrowed safely. Restore supported reducer bodies and rebuild or migrate retained data before recovery.";
        return availability.Reason(query.ReadModel) is null ? null : "The query's read model is quarantined by an unavailable reducer; snapshot and live delivery are unavailable.";
    }

    internal static void EmitPerformer(AxonEmissionContext context, string package, SemanticKeyedQuery query, string modelType, string reason)
    {
        var name = JavaNames.Type(query.Name);
        var code = QueryModelReference.Create(context, package).Declare(name).Declare(name + "Performer");
        var model = QueryModelReference.Claim(code, package, modelType, name, name + "Performer");
        var store = code.Reference(AxonRuntimePackage.Type(context, "ReadModelStore"));
        code.UnsupportedIntent(reason, query.Id.ToString(), AxonRefusalCodes.Reduction)
            .Block($"public interface {name}Performer", body => body
                .Block($"default {body.Reference("java.util.List")}<{model}> perform({name} query, {store}<{body.Reference("java.lang.Object")}, {model}> store)", method => method
                    .Line($"throw new {method.Reference("java.lang.IllegalStateException")}({JavaCodeBuilder.StringLiteral(UnavailableReadModelStore.Message)});")));
        context.AddJava(package, name + "Performer", code.Build(), sources: query.Id);
        var configuration = new JavaCodeBuilder(package).Declare(name + "Performer").Declare(name + "PerformerConfiguration");
        configuration.Line($"@{configuration.Reference("org.springframework.context.annotation.Configuration")}({JavaCodeBuilder.StringLiteral(package + "." + name + "PerformerConfiguration")})")
            .Block($"public class {name}PerformerConfiguration", body => body
                .Line($"@{body.Reference("org.springframework.context.annotation.Bean")}({JavaCodeBuilder.StringLiteral(package + "." + name + "Performer")})")
                .Line($"@{body.Reference("org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean")}({name}Performer.class)")
                .Block($"public {name}Performer {JavaNames.Member(name)}Performer()", method => method.Line($"return new {name}Performer() {{}};")));
        context.AddJava(package, name + "PerformerConfiguration", configuration.Build(), sources: query.Id);
    }
}
