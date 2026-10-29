// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

internal static class HttpSecurityExamples
{
    internal static string Body(AxonEmissionContext context, HttpEndpoint endpoint)
    {
        var command = ScopeSelection.Select(context.Request).SelectMany(slice => slice.Slice.Commands).FirstOrDefault(command => command.Id == endpoint.Source);
        return command is null ? "{}" : JsonSerializer.Serialize(command.Properties.ToDictionary(property => JavaNames.Member(property.Name), property => Value(context, property.Type, 0)));
    }

    internal static string Parameter(AxonEmissionContext context, HttpParameter parameter) => parameter.Type is { } type
        ? Convert.ToString(Value(context, type with { IsCollection = false }, 0), System.Globalization.CultureInfo.InvariantCulture) ?? "x" : "x";

    internal static object? Value(AxonEmissionContext context, SemanticTypeReference type, int depth)
    {
        if (HttpModeledExamples.Value(context, type, out var example)) return example;
        if (type.IsCollection) return Array.Empty<object>();
        if (type.Kind == SemanticTypeReferenceKind.CompositeType) return CompositeValue(context, type, depth);
        if (type.Kind == SemanticTypeReferenceKind.Concept) return ConceptValue(context, type);
        return PrimitiveValue(context, type);
    }

    internal static bool CanSupply(AxonEmissionContext context, HttpEndpoint endpoint)
    {
        var command = ScopeSelection.Select(context.Request).SelectMany(owner => owner.Slice.Commands).FirstOrDefault(command => command.Id == endpoint.Source);
        var properties = command?.Properties.Select(property => property.Type) ?? [];
        if (properties.Concat(endpoint.Parameters.Where(parameter => parameter.Type is not null).Select(parameter => parameter.Type!)).All(type => Safe(context, type, 0))) return true;
        context.Degradation.Warn("STAGE-AXON-HTTP-SPEC", endpoint.Source ?? context.Request.Model.Application.Id, $"Skipped HTTP security assertions for {endpoint.Method} {endpoint.Path}: no valid modeled request example is available.");
        return false;
    }

    static bool Safe(AxonEmissionContext context, SemanticTypeReference type, int depth)
    {
        if (depth > 8) return false;
        if (HttpModeledExamples.Value(context, type, out _)) return true;
        if (type.IsCollection || type.Kind == SemanticTypeReferenceKind.Primitive) return true;
        if (type.Kind == SemanticTypeReferenceKind.Concept)
            return !context.Request.Model.Application.Concepts.First(item => item.Id == type.Target).Values.IsDefaultOrEmpty;
        return context.Request.Model.Application.Types.First(item => item.Id == type.Target).Properties.All(property => Safe(context, property.Type, depth + 1));
    }

    static Dictionary<string, object?>? CompositeValue(AxonEmissionContext context, SemanticTypeReference type, int depth)
    {
        if (depth > 8) return null;
        var composite = context.Request.Model.Application.Types.First(item => item.Id == type.Target);
        return composite.Properties.ToDictionary(property => JavaNames.Member(property.Name), property => Value(context, property.Type, depth + 1));
    }

    static object ConceptValue(AxonEmissionContext context, SemanticTypeReference type)
    {
        var concept = context.Request.Model.Application.Concepts.First(item => item.Id == type.Target);
        if (concept.Values.IsDefaultOrEmpty) return PrimitiveValue(context, type);
        return concept.Numbers is { } numbers ? numbers[0] : concept.Values[0];
    }

    static object PrimitiveValue(AxonEmissionContext context, SemanticTypeReference type) =>
        new CommandJavaTypes(context.Request.Model.Application, context.BasePackage).PrimitiveOf(type) switch
        {
            SemanticPrimitiveType.Uuid => "00000000-0000-0000-0000-000000000001",
            SemanticPrimitiveType.Boolean => true,
            SemanticPrimitiveType.WholeNumber => 1L,
            SemanticPrimitiveType.DecimalNumber => 1m,
            var primitive => TemporalOrText(primitive)
        };

    static string TemporalOrText(SemanticPrimitiveType primitive) => primitive switch
    {
        SemanticPrimitiveType.Date => "2026-01-01",
        SemanticPrimitiveType.DateTime => "2026-01-01T00:00:00+00:00",
        _ => "http-contract"
    };
}
