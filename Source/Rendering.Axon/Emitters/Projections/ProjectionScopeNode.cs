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

    /// <summary>
    /// Whether every nested and child scope of the projection resolves to a composite property of the read model.
    /// </summary>
    /// <param name="application">The semantic application.</param>
    /// <param name="projection">The projection.</param>
    /// <param name="model">The projected read model.</param>
    /// <returns>True when the whole scope tree can be built.</returns>
    internal static bool Resolvable(SemanticApplication application, SemanticProjection projection, SemanticReadModel model) =>
        projection.Scope is not { } scope || Resolvable(application, model.Properties, scope);

    static bool Resolvable(SemanticApplication application, IReadOnlyList<SemanticProperty> properties, SemanticProjectionScope scope)
    {
        foreach (var nested in scope.Nested)
        {
            if (Composite(application, properties, nested.Property) is not { } composite || !Resolvable(application, composite.Properties, nested.Scope)) return false;
        }
        foreach (var children in scope.Children)
        {
            if (Composite(application, properties, children.Property) is not { } composite) return false;
            if (composite.Properties.All(property => property.Id != children.IdentifiedBy) || !Resolvable(application, composite.Properties, children.Scope)) return false;
        }
        return true;
    }

    static SemanticCompositeType? Composite(SemanticApplication application, IReadOnlyList<SemanticProperty> properties, SemanticId id)
    {
        var property = properties.FirstOrDefault(candidate => candidate.Id == id);
        return property is { Type.Kind: SemanticTypeReferenceKind.CompositeType } ? application.Types.FirstOrDefault(candidate => candidate.Id == property.Type.Target) : null;
    }

    static void Populate(ProjectionScopeNode node, SemanticApplication application, ViewJavaTypes types)
    {
        foreach (var nested in node.Scope.Nested)
        {
            if (Composite(application, node.Properties, nested.Property) is not { } composite) continue;
            var property = node.Properties.First(candidate => candidate.Id == nested.Property);
            var child = new ProjectionScopeNode(node.Name + "Nested" + node.Nested.Count, types.BasePackage + ".types." + Java.JavaNames.Type(composite.Name), composite.Properties, nested.Scope, property);
            node.Nested.Add(child);
            Populate(child, application, types);
        }
        foreach (var children in node.Scope.Children)
        {
            if (Composite(application, node.Properties, children.Property) is not { } composite) continue;
            var property = node.Properties.First(candidate => candidate.Id == children.Property);
            var child = new ProjectionScopeNode(node.Name + "Child" + node.Children.Count, types.BasePackage + ".types." + Java.JavaNames.Type(composite.Name), composite.Properties, children.Scope, property, children.IdentifiedBy);
            node.Children.Add(child);
            Populate(child, application, types);
        }
    }
}
