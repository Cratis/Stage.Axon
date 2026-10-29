// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_http_cardinality_tests : a_query_collision
{
    string _test;

    void Because()
    {
        _query = _query with { Cardinality = SemanticQueryCardinality.ZeroOrOne };
        Render();
        AxonHttpCardinalityTests.Emit(_context);
        _test = Code("HttpOptionalQueryContractTest");
    }

    [Fact] void should_call_the_generated_query_over_http() => _test.ShouldContain("http.perform(get(\"/api/views/counters/find-total\")");
    [Fact] void should_clear_the_repository() => _test.ShouldContain("repository.deleteAll();");
    [Fact] void should_test_two_rows() => _test.ShouldContain("repository.saveAll(List.of(new TotalEntity(\"cardinality-row-1\", row), new TotalEntity(\"cardinality-row-2\", row)));");
    [Fact] void should_assert_an_absent_optional_result_has_an_empty_body() => _test.ShouldContain("perform().andExpect(status().isNotFound()).andExpect(content().string(\"\"));");
    [Fact] void should_assert_the_cardinality_problem() => _test.ShouldContain("urn:cratis:stage-axon:problem:query-cardinality");
    [Fact] void should_assert_the_count() => _test.ShouldContain("jsonPath(\"$.count\").value(2)");
    [Fact] void should_assert_row_values_are_not_leaked() => _test.ShouldContain("not(containsString(mapper.writeValueAsString(row)))");
    [Fact] void should_not_depend_on_runtime_cardinality_helpers() => _test.ShouldNotContain("QueryResults");
}
