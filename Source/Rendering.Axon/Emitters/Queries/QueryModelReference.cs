// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries;

/// <summary>Reserves package types before claiming query framework and read-model references.</summary>
internal static class QueryModelReference
{
    internal static JavaCodeBuilder Create(AxonEmissionContext context, string package)
    {
        var code = new JavaCodeBuilder(package);
        var types = new ViewJavaTypes(context.Request.Model.Application);
        foreach (var located in ScopeSelection.Select(context.Request).Where(located => types.Package(located) == package))
        {
            foreach (var model in located.Slice.ReadModels) code.Declare(JavaNames.Type(model.Name));
            foreach (var query in located.Slice.Queries.Concat(QueryPerformer.Queries(context, located)))
            {
                foreach (var name in QueryNames(context, query)) code.Declare(name);
            }
        }
        var prefix = "src/main/java/" + package.Replace('.', '/') + "/";
        foreach (var name in context.Artifacts.Where(artifact => artifact.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(artifact => artifact.RelativePath[prefix.Length..])
            .Where(name => name.EndsWith(".java", StringComparison.Ordinal) && !name.Contains('/')))
        {
            code.Declare(name[..^5]);
        }
        return code;
    }

    internal static void ReserveLocal(JavaCodeBuilder code, string package, string modelType, params string[] declared)
    {
        var separator = modelType.LastIndexOf('.');
        var name = modelType[(separator + 1)..];
        if (modelType[..separator] == package && !declared.Contains(name, StringComparer.Ordinal)) code.Declare(name);
    }

    internal static string Claim(JavaCodeBuilder code, string package, string modelType, params string[] declared)
    {
        var separator = modelType.LastIndexOf('.');
        var name = modelType[(separator + 1)..];
        if (modelType[..separator] != package || declared.Contains(name, StringComparer.Ordinal)) return code.Reference(modelType);
        code.Declare(name);
        return name;
    }

    static IEnumerable<string> QueryNames(AxonEmissionContext context, SemanticKeyedQuery query)
    {
        var name = JavaNames.Type(query.Name);
        yield return name;
        yield return name + "Handler";
        yield return name + "Endpoint";
        if (QueryPerformer.Required(context, query))
        {
            yield return name + "Performer";
            yield return name + "PerformerConfiguration";
        }
        if (query.Delivery == SemanticQueryDelivery.Live)
        {
            yield return name + "Subscription";
            yield return name + "Streams";
        }
    }
}
