// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries.for_AxonQueriesEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Queries.for_QueryEndpoint;

public class when_a_query_is_named_pre_authorize : a_query_collision
{
    string _endpoint;
    string _test;

    async Task Because()
    {
        var orders = await for_AxonArtifactRenderPlanner.given.blueprint_models.Load("orders");
        var policy = orders.Model.Application.Policies.First(candidate => candidate.Name == "IsSales");
        var application = _request.Model.Application with { Policies = _request.Model.Application.Policies.Add(policy) };
        _request = _request with { Model = ExecutableSemanticModel.Create(_request.Model.LanguageVersion, _request.Model.SemanticVersion, application) };
        _query = _query with { Name = "PreAuthorize", Authorization = new SemanticPolicyReference("IsSales") };
        Render();
        AxonHttpTests.Emit(_context);
        _endpoint = Code("PreAuthorizeEndpoint");
        _test = Code("HttpContractRoutesTest");
    }

    [Fact] void should_qualify_the_annotation_because_of_the_collision() => _endpoint.ShouldContain("@org.springframework.security.access.prepost.PreAuthorize(\"hasRole('Sales')\")");
    [Fact] void should_still_carry_the_expression_into_the_contract() => AxonHttpTestsEndpoints.For(_context).Where(endpoint => endpoint.Kind == "query").All(endpoint => endpoint.Authorization == "hasRole('Sales')").ShouldBeTrue();
    [Fact] void should_still_emit_the_denial_checks() => _test.ShouldContain("protected_endpoints_deny_anonymous_and_unauthorized_callers");
    [Fact] void should_contain_the_contract_file() => Encoding.UTF8.GetString(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("http-contract.json", StringComparison.Ordinal)).Bytes.AsSpan()).ShouldContain("\"authorization\":\"hasRole('Sales')\"");
}
