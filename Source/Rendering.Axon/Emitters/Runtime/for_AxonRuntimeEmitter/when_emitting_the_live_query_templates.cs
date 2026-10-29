// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_the_live_query_templates : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new AxonEmissionContext(request, new(request.Profile));
        new AxonRuntimeEmitter().Emit(_context);
    }

    [Fact] void should_not_emit_the_deleted_live_contract_template() => _context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/LiveQueryContractTest.java", StringComparison.Ordinal)).ShouldBeFalse();
}
