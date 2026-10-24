// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_reducer_delegates : a_view_request
{
    AxonEmissionContext _context;
    string _contract;
    string _projector;
    string _specs;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        _contract = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducer.java", StringComparison.Ordinal)));
        _projector = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducerProjector.java", StringComparison.Ordinal)));
        _specs = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_expose_a_typed_reduction() => _contract.ShouldContain("reduce(axonviews.reduction.events.CountRecorded event, RebuiltTotal current");
    [Fact] void should_not_translate_csharp_into_invalid_java() => _contract.ShouldContain("throw new UnsupportedOperationException");
    [Fact] void should_preserve_the_reducer_intent() => _contract.ShouldContain("Screenplay wanted to reduce this event and current state");
    [Fact] void should_delegate_from_an_event_handler() => _projector.ShouldContain("var next = reducer.reduce(event, store.get(key).orElse(null), message);");
    [Fact] void should_delete_when_the_reducer_returns_no_state() => _projector.ShouldContain("if (next == null) store.remove(key); else store.put(key, next);");
    [Fact] void should_disable_dependent_specs() => _specs.ShouldContain("@org.junit.jupiter.api.Disabled(\"STAGE-AXON-0306:");
    [Fact] void should_warn_that_the_body_needs_an_implementation() => _context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0306").ShouldBeTrue();

    [Fact]
    void should_explain_why_a_missing_body_cannot_be_inferred()
    {
        var request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducer.java", StringComparison.Ordinal))).ShouldContain("no available transition body");
    }
}
