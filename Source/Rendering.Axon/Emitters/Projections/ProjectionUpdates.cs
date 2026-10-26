// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionUpdates(AxonEmissionContext context, ViewJavaTypes types, SemanticId model)
{
    readonly (LocatedSemanticSlice Owner, SemanticKeyedQuery Query)[] _queries = [.. ScopeSelection.Locate(context.Request.Model.Application)
        .SelectMany(owner => owner.Slice.Queries.Concat(QueryPerformer.Queries(context, owner)).Where(query => query.ReadModel == model && query.Delivery == SemanticQueryDelivery.Live).Select(query => (owner, query)))];

    internal bool Enabled => _queries.Length > 0;

    internal string Parameter => Enabled ? ", org.axonframework.messaging.queryhandling.QueryUpdateEmitter updates" : string.Empty;

    internal string DefaultArgument => Enabled ? ", null" : string.Empty;

    internal string ParameterFor(JavaCodeBuilder code) => Enabled ? $", {code.Reference("org.axonframework.messaging.queryhandling.QueryUpdateEmitter")} updates" : string.Empty;

    internal void Claim(JavaCodeBuilder code, string package)
    {
        foreach (var (owner, query) in _queries) QueryType(owner, query, code, package);
    }

    internal void Notify(JavaCodeBuilder code, string package) => NotifyQueries(code, package);

    internal void Notify(JavaCodeBuilder code) => NotifyQueries(code, null);

    void NotifyQueries(JavaCodeBuilder code, string? package)
    {
        if (!Enabled) return;
        code.Block("if (updates != null)", body =>
        {
            foreach (var (owner, query) in _queries)
            {
                var name = package is null ? types.Package(owner) + "." + JavaNames.Type(query.Name) : QueryType(owner, query, body, package);
                body.Line($"updates.emit({name}.class, query -> true, {name}.Result::invalidation);");
            }
        });
    }

    string QueryType(LocatedSemanticSlice owner, SemanticKeyedQuery query, JavaCodeBuilder code, string package)
    {
        var name = JavaNames.Type(query.Name);
        var queryPackage = types.Package(owner);
        if (queryPackage == package) code.Declare(name);
        else code.Import(queryPackage + "." + name);
        return name;
    }
}
