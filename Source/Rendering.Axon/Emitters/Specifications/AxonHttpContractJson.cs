// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications;

/// <summary>Emits the language-neutral <c>http-contract.json</c> from the generated endpoint registry.</summary>
internal static class AxonHttpContractJson
{
    internal const string Path = "src/main/resources/http-contract.json";

    static readonly JsonSerializerOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static void Emit(AxonEmissionContext context)
    {
        var endpoints = AxonHttpTestsEndpoints.For(context);
        if (endpoints.Count == 0) return;
        var lines = endpoints.Select(endpoint => "    " + JsonSerializer.Serialize(Describe(endpoint), _options));
        context.Artifacts.Add(PlannedArtifact.CreateText(Path, "{\n  \"version\": 1,\n  \"endpoints\": [\n" + string.Join(",\n", lines) + "\n  ]\n}\n"));
    }

    internal static IReadOnlyList<(string Type, int Status)> Problems(HttpEndpoint endpoint) => endpoint switch
    {
        { Kind: "stream" } => [("live-capacity", 503)],
        { Kind: "command" } => [("validation", 400), ("conflict", 409), ("invalid-argument", 400), ("unsupported", 501)],
        { Cardinality: "one" } => [("query-result-missing", 404), ("query-cardinality", 500)],
        { Cardinality: "zeroOrOne" } => [("query-cardinality", 500)],
        _ => []
    };

    static Dictionary<string, object?> Describe(HttpEndpoint endpoint)
    {
        var stream = endpoint.Kind == "stream";
        return new Dictionary<string, object?>
        {
            ["method"] = endpoint.Method,
            ["path"] = endpoint.Path,
            ["produces"] = endpoint.Produces is null ? Array.Empty<string>() : [endpoint.Produces],
            ["kind"] = endpoint.Kind,
            ["consumes"] = endpoint.Kind == "command" ? new[] { "application/json" } : [],
            ["responses"] = stream ? new[] { "text/event-stream" } : ["application/json"],
            ["parameters"] = endpoint.Parameters.Select(parameter => new { name = parameter.Name, location = "query", required = parameter.Required }).ToArray(),
            ["cardinality"] = endpoint.Cardinality,
            ["authorization"] = endpoint.Authorization,
            ["problems"] = Problems(endpoint).Select(problem => new { type = "urn:cratis:stage-axon:problem:" + problem.Type, status = problem.Status }).ToArray()
        };
    }
}
