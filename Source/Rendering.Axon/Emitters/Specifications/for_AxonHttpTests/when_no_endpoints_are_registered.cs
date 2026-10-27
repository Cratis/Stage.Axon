// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests;

public class when_no_endpoints_are_registered : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new(request, new(request.Profile));
        AxonHttpTestsContract.Emit(_context);
        AxonHttpContractJson.Emit(_context);
    }

    [Fact] void should_not_emit_a_route_test() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/HttpContractRoutesTest.java", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_emit_a_contract_file() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("http-contract.json", StringComparison.Ordinal)).ShouldBeFalse();
}
