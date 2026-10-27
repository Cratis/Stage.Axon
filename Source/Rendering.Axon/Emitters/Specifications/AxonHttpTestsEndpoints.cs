// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

/// <summary>A request parameter of a generated HTTP endpoint.</summary>
/// <param name="Name">The query parameter name.</param>
/// <param name="Required">Whether the parameter must be supplied.</param>
internal sealed record HttpParameter(string Name, bool Required);

/// <summary>A generated HTTP endpoint, as registered by the command and query emitters.</summary>
/// <param name="Kind">The endpoint kind: command, query or stream.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The route.</param>
/// <param name="Produces">The media type declared on the mapping, if any.</param>
/// <param name="Authorization">The authorization expression, if any.</param>
/// <param name="Parameters">The request parameters.</param>
/// <param name="Cardinality">The query cardinality, if a query.</param>
internal sealed record HttpEndpoint(string Kind, string Method, string Path, string? Produces, string? Authorization, IReadOnlyList<HttpParameter> Parameters, string? Cardinality);

/// <summary>Registry of the HTTP endpoints generated for an emission, keyed by the emission context.</summary>
internal static class AxonHttpTestsEndpoints
{
    const string Annotation = "@PreAuthorize(\"";
    static readonly ConditionalWeakTable<AxonEmissionContext, List<HttpEndpoint>> _endpoints = [];

    internal static void Register(AxonEmissionContext context, HttpEndpoint endpoint) => _endpoints.GetOrCreateValue(context).Add(endpoint);

    internal static IReadOnlyList<HttpEndpoint> For(AxonEmissionContext context) => _endpoints.TryGetValue(context, out var endpoints)
        ? [.. endpoints.OrderBy(endpoint => endpoint.Path, StringComparer.Ordinal).ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal).ThenBy(endpoint => endpoint.Produces, StringComparer.Ordinal)]
        : [];

    /// <summary>Extracts the authorization expression from a rendered PreAuthorize annotation.</summary>
    /// <param name="annotation">The rendered annotation text; may be empty.</param>
    /// <returns>The expression, or null when there is none.</returns>
    internal static string? Expression(string annotation)
    {
        var start = annotation.LastIndexOf(Annotation, StringComparison.Ordinal);
        if (start < 0) return null;
        var end = annotation.LastIndexOf("\")", StringComparison.Ordinal);
        return end <= start ? null : annotation[(start + Annotation.Length)..end].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}
