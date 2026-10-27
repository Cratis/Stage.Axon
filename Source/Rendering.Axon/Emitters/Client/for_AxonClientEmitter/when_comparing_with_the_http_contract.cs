// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_comparing_with_the_http_contract : given.a_client_request
{
    List<JsonElement> contract;
    List<JsonElement> routes;

    static string Key(JsonElement entry) => entry.GetProperty("method").GetString() + " " + entry.GetProperty("path").GetString() + (entry.GetProperty("produces").GetArrayLength() == 0 ? string.Empty : " " + entry.GetProperty("produces")[0].GetString());

    void Because()
    {
        Render();
        contract = [.. JsonDocument.Parse(Text("src/main/resources/http-contract.json")).RootElement.GetProperty("endpoints").EnumerateArray()];
        routes = [.. Text("client/src/http-routes.ts").Split('\n').Where(line => line.StartsWith("    {", StringComparison.Ordinal)).Select(line => JsonDocument.Parse(line.Trim().TrimEnd(',')).RootElement)];
    }

    [Fact] void should_have_the_same_count() => routes.Count.ShouldEqual(contract.Count);
    [Fact] void should_have_exactly_one_route_per_entry() => contract.TrueForAll(entry => routes.Count(route => route.GetProperty("id").GetString() == Key(entry)) == 1).ShouldBeTrue();
    [Fact] void should_match_every_entry_exactly() => contract.TrueForAll(entry => routes.Single(route => route.GetProperty("id").GetString() == Key(entry)) is var route && new[] { "method", "path", "produces", "kind", "consumes", "responses", "parameters", "cardinality", "authorization", "problems" }.All(name => route.GetProperty(name).GetRawText() == entry.GetProperty(name).GetRawText())).ShouldBeTrue();
    [Fact] void should_keep_the_json_and_stream_routes_apart() => routes.Select(route => route.GetProperty("id").GetString()).ShouldContain(id => id!.EndsWith("text/event-stream", StringComparison.Ordinal));
    [Fact] void should_mark_void_commands() => routes.Exists(route => route.GetProperty("kind").GetString() == "command" && route.GetProperty("response").GetString() == "none").ShouldBeTrue();
}
