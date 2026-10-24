// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed record ProjectionScopeNode(string Name, string Type, IReadOnlyList<SemanticProperty> Properties, SemanticProjectionScope Scope, SemanticProperty? Property = null, SemanticId? Identity = null)
{
    internal List<ProjectionScopeNode> Nested { get; } = [];
    internal List<ProjectionScopeNode> Children { get; } = [];
    internal IEnumerable<ProjectionScopeNode> All => new[] { this }.Concat(Nested.Concat(Children).SelectMany(node => node.All));

    internal static ProjectionScopeNode Create(SemanticApplication application, ViewJavaTypes types, SemanticProjection projection, SemanticReadModel model, string type)
    {
        var root = new ProjectionScopeNode("Root", type, model.Properties, projection.Scope ?? SemanticProjectionScope.Empty with { From = [.. ProjectionTransitions.From(projection)] });
        Populate(root, application, types);
        return root;
    }

    static void Populate(ProjectionScopeNode node, SemanticApplication application, ViewJavaTypes types)
    {
        foreach (var nested in node.Scope.Nested)
        {
            var property = node.Properties.First(candidate => candidate.Id == nested.Property);
            var composite = application.Types.First(candidate => candidate.Id == property.Type.Target);
            var child = new ProjectionScopeNode(node.Name + "Nested" + node.Nested.Count, types.BasePackage + ".types." + Java.JavaNames.Type(composite.Name), composite.Properties, nested.Scope, property);
            node.Nested.Add(child);
            Populate(child, application, types);
        }
        foreach (var children in node.Scope.Children)
        {
            var property = node.Properties.First(candidate => candidate.Id == children.Property);
            var composite = application.Types.First(candidate => candidate.Id == property.Type.Target);
            var child = new ProjectionScopeNode(node.Name + "Child" + node.Children.Count, types.BasePackage + ".types." + Java.JavaNames.Type(composite.Name), composite.Properties, children.Scope, property, children.IdentifiedBy);
            node.Children.Add(child);
            Populate(child, application, types);
        }
    }
}
