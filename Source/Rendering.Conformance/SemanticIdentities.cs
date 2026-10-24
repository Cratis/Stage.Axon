// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Conformance;

internal static class SemanticIdentities
{
    internal static IReadOnlyDictionary<SemanticId, object> Definitions(ExecutableSemanticModel model) =>
        Walk(model.Application).Where(value => Identity(value).IsSet).GroupBy(Identity).ToDictionary(group => group.Key, group => group.First());

    internal static IEnumerable<SemanticId> Reachable(ArtifactRenderRequest request)
    {
        var definitions = Definitions(request.Model);
        var visited = new HashSet<SemanticId>();
        var queue = new Queue<SemanticId>(new[] { request.Scope }.Concat(request.AdditionalScopes).Select(scope => scope.Artifact));
        while (queue.TryDequeue(out var identity))
        {
            if (!visited.Add(identity)) continue;
            if (!definitions.TryGetValue(identity, out var root)) continue;
            yield return identity;
            foreach (var reference in Walk(root).OfType<SemanticId>().Where(definitions.ContainsKey)) queue.Enqueue(reference);
        }
    }

    static SemanticId Identity(object value) => value.GetType().GetProperty("Id")?.GetValue(value) is SemanticId identity ? identity : default;

    static IEnumerable<object> Walk(object? value)
    {
        if (value is null) yield break;
        yield return value;
        foreach (var child in Children(value).SelectMany(Walk)) yield return child;
    }

    static IEnumerable<object?> Children(object value)
    {
        if (value is SemanticId || value is string) return [];
        if (value is IEnumerable collection) return collection.Cast<object?>();
        var type = value.GetType();
        if (type.Namespace != typeof(SemanticApplication).Namespace) return [];
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0).Select(property => property.GetValue(value));
    }
}
