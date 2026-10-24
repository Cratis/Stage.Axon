// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_flat_views : given.a_view_request
{
    AxonEmissionContext _context;
    string _projector;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        _projector = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("TotalProjectionProjector.java", StringComparison.Ordinal)));
    }

    [Fact] void should_emit_an_immutable_model() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Total.java", StringComparison.Ordinal))).ShouldContain("public record Total(");
    [Fact] void should_use_an_atomic_store_update() => _projector.ShouldContain("store.update(\"global\", current -> {");
    [Fact] void should_emit_both_event_handlers() => _projector.Split("@EventHandler", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_add_the_event_value() => _projector.ShouldContain("+ event.amount()");
    [Fact] void should_subtract_the_event_value() => _projector.ShouldContain("- event.amount()");
    [Fact] void should_clear_the_optional_property() => _projector.ShouldContain("state.count(), null)");
    [Fact] void should_configure_subscribing_processing() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/axon-views.properties")).ShouldContain(".mode=subscribing");
    [Fact] void should_use_a_model_specific_store_qualifier() => _projector.ShouldContain("@Qualifier(\"axonviews.views.counters.totals.Total\")");
    [Fact] void should_have_no_degradations() => _context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
}
