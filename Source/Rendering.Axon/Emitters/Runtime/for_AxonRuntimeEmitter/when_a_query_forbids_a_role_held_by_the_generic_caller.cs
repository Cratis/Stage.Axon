// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_a_query_forbids_a_role_held_by_the_generic_caller : a_query_collision
{
    string _test;

    void Because()
    {
        var application = _request.Model.Application with
        {
            Policies = _request.Model.Application.Policies.Add(new("NotBlocked", new SemanticNotPolicyCondition(new SemanticRoleCondition("Blocked"))))
        };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _query = _query with { Cardinality = SemanticQueryCardinality.ZeroOrOne, Authorization = new SemanticPolicyReference("NotBlocked") };
        Render();
        AxonHttpCardinalityTests.Emit(_context);
        _test = Code("HttpOptionalQueryContractTest");
    }

    [Fact] void should_not_use_an_unauthorized_query_to_test_cardinality() => _test.ShouldNotContain("http.perform(get(\"/api/views/counters/find-total\")");
}
