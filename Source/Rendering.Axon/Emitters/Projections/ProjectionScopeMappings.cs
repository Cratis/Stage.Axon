// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class ProjectionScopeMappings(AxonEmissionContext context, ViewJavaTypes types, SemanticId owner)
{
    readonly ProjectionRecords _records = new(context.Request.Model.Application, types);
    readonly ProjectionValues _values = new(context, types);

    internal void Apply(JavaCodeBuilder code, ProjectionScopeNode node, IEnumerable<SemanticProjectionMapping> mappings, SemanticEventContract @event)
    {
        foreach (var mapping in mappings)
        {
            var target = _records.Property(node.Properties, mapping.Target);
            var value = target is null ? null : _values.Mapping(mapping, target, @event, _records.Access(node.Properties, mapping.Target, "state"));
            if (value is null)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.ProjectionMapping, owner, "The mapping value is not available in the Axon target.", "map the projected property", owner + "/mapping");
                code.UnsupportedIntent("map the projected property", owner.ToString(), AxonRefusalCodes.ProjectionMapping);
                continue;
            }
            code.Line($"state = {_records.Set(node.Type, node.Properties, mapping.Target, "state", value)};");
        }
    }

    internal void Identify(JavaCodeBuilder code, ProjectionScopeNode node, SemanticProjectionFrom from, SemanticEventContract @event)
    {
        if (from.Key is SemanticProjectionPartsKey parts)
        {
            Apply(code, node, parts.Parts.Select(part => new SemanticProjectionMapping([part.Property], SemanticProjectionOperation.Set, part.Value)), @event);
            return;
        }
        SemanticProperty[] identifiers = node.Identity is { } identity ? [.. node.Properties.Where(property => property.Id == identity)] : [.. node.Properties.Where(property => property.IsIdentifier)];
        if (identifiers.Length != 1 || identifiers[0].Type.Kind == SemanticTypeReferenceKind.CompositeType) return;
        var property = identifiers[0];
        code.Line($"state = {_records.Set(node.Type, node.Properties, [property.Id], "state", types.Parse(property.Type, "String.valueOf(key)"))};");
    }
}
