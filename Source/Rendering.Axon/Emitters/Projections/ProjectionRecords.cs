// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionRecords(SemanticApplication application, ViewJavaTypes types, JavaCodeBuilder? code = null)
{
    internal string Empty(string type, IReadOnlyList<SemanticProperty> properties) => Resolve($"new {type}({string.Join(", ", properties.Select(property => types.Default(property.Type)))})");

    internal SemanticProperty? Property(IReadOnlyList<SemanticProperty> properties, IReadOnlyList<SemanticId> path)
    {
        SemanticProperty? result = null;
        foreach (var id in path)
        {
            result = properties.FirstOrDefault(property => property.Id == id);
            if (result is null) return null;
            properties = application.Types.FirstOrDefault(type => type.Id == result.Type.Target)?.Properties ?? [];
        }
        return result;
    }

    internal string Access(IReadOnlyList<SemanticProperty> properties, IReadOnlyList<SemanticId> path, string receiver)
    {
        foreach (var id in path)
        {
            var property = properties.First(candidate => candidate.Id == id);
            receiver += "." + JavaNames.Member(property.Name) + "()";
            if (property.Type.Kind == SemanticTypeReferenceKind.CompositeType && !property.Type.IsCollection)
            {
                var composite = application.Types.First(type => type.Id == property.Type.Target);
                receiver = $"({receiver} == null ? {Empty(types.Type(property.Type), composite.Properties)} : {receiver})";
                properties = composite.Properties;
            }
        }
        return Resolve(receiver);
    }

    internal string Set(ProjectionScopeNode node, IReadOnlyList<SemanticId> path, string value)
    {
        var property = node.Properties.First(candidate => candidate.Id == path[0]);
        if (path.Count > 1)
        {
            var composite = application.Types.First(candidate => candidate.Id == property.Type.Target);
            value = Set(types.Type(property.Type), composite.Properties, [.. path.Skip(1)], Access(node.Properties, [property.Id], "state"), value);
        }
        return node.Property is null
            ? $"state.with{JavaNames.Type(property.Name)}({value})"
            : Set(node.Type, node.Properties, [property.Id], "state", value);
    }

    internal string Set(string type, IReadOnlyList<SemanticProperty> properties, IReadOnlyList<SemanticId> path, string receiver, string value)
    {
        var property = properties.First(candidate => candidate.Id == path[0]);
        if (path.Count > 1)
        {
            var composite = application.Types.First(candidate => candidate.Id == property.Type.Target);
            value = Set(types.Type(property.Type), composite.Properties, [.. path.Skip(1)], Access(properties, [property.Id], receiver), value);
        }
        var arguments = properties.Select(candidate => candidate.Id == property.Id ? value : $"{receiver}.{JavaNames.Member(candidate.Name)}()");
        return Resolve($"new {type}({string.Join(", ", arguments)})");
    }

    string Resolve(string expression) => code?.ReferenceExpression(expression) ?? expression;
}
