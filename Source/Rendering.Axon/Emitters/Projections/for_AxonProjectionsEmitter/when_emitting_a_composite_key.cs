// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_composite_key : given.a_view_request
{
    AxonEmissionContext _context;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
    }

    [Fact] void should_not_limit_the_composite_key_length() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/MetricEntity.java", StringComparison.Ordinal))).ShouldContain("@Column(name = \"row_key\", columnDefinition = \"VARCHAR(1000000000)\")");
    [Fact] void should_not_limit_a_single_key_length() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalEntity.java", StringComparison.Ordinal))).ShouldContain("@Column(name = \"row_key\", columnDefinition = \"VARCHAR(1000000000)\")");
}
