// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_emitting_the_route_contract : a_query_collision
{
    string _test;
    string _json;

    void Because()
    {
        _query = _query with { Delivery = SemanticQueryDelivery.Live, Cardinality = SemanticQueryCardinality.One };
        Render();
        AxonHttpTests.Emit(_context);
        _test = Code("HttpContractRoutesTest");
        _json = Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/http-contract.json").Bytes.AsSpan());
    }

    [Fact] void should_list_the_json_route() => _test.ShouldContain("\"GET /api/views/counters/find-total\"");
    [Fact] void should_list_the_stream_route_with_its_media_type() => _test.ShouldContain("\"GET /api/views/counters/find-total text/event-stream\"");
    [Fact] void should_not_exclude_routes_by_url_prefix() => _test.ShouldNotContain("/api/triggers/");
    [Fact] void should_compare_the_contract_file_to_the_live_mappings() => _test.ShouldContain("getResourceAsStream(\"/http-contract.json\")");
    [Fact] void should_describe_the_route_in_the_contract_file() => _json.ShouldContain("\"method\":\"GET\",\"path\":\"/api/views/counters/find-total\",\"produces\":[]");
    [Fact] void should_describe_the_stream_in_the_contract_file() => _json.ShouldContain("\"produces\":[\"text/event-stream\"]");
    [Fact] void should_describe_the_cardinality() => _json.ShouldContain("\"cardinality\":\"one\"");
    [Fact] void should_describe_the_problem_types() => _json.ShouldContain("\"type\":\"urn:cratis:stage-axon:problem:query-result-missing\",\"status\":404");
    [Fact] void should_not_use_fully_qualified_names_in_the_test_body() => _test.ShouldNotContain("org.springframework.test.web.servlet.MockMvc http");
    [Fact] void should_not_carry_a_license_header() => _test.ShouldNotContain("Licensed under");
}
