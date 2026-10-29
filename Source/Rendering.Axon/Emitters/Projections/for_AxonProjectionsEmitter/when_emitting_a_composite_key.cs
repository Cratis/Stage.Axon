// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_composite_key : given.a_view_request
{
    AxonEmissionContext _context;
    string _projector;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        _projector = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/MetricProjectionProjector.java", StringComparison.Ordinal)));
    }

    [Fact] void should_construct_the_embedded_key_in_model_order() => _projector.ShouldContain("var key = new MetricKey(event.region(), event.month());");
    [Fact] void should_remove_by_the_embedded_key() => _projector.ShouldContain("repository.deleteAllByIdInBatch(List.of(new MetricKey(event.region(), event.month())));");
    [Fact] void should_write_the_model_with_the_embedded_key() => _projector.ShouldContain("repository.save(new MetricEntity(key, state));");
    [Fact] void should_not_degrade_the_composite_projection() => _projector.ShouldNotContain("STAGE-AXON-0310");
    [Fact] void should_handle_both_events() => _projector.ShouldContain("handle(MetricRemoved event");
    [Fact] void should_not_limit_a_single_key_length() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalEntity.java", StringComparison.Ordinal))).ShouldContain("@Column(name = \"row_key\", columnDefinition = \"VARCHAR(1000000000)\")");
}
