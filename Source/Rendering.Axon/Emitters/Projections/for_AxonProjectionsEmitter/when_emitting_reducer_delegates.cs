// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Events;
using Cratis.Stage.Rendering.Axon.Emitters.Implementations;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
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
        new AxonEventsEmitter().Emit(_context);
        _contract = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducer.java", StringComparison.Ordinal)));
        _projector = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducerProjector.java", StringComparison.Ordinal)));
        _specs = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalViewSpecificationsTest.java", StringComparison.Ordinal)));
    }

    [Fact] void should_expose_a_nullable_current() => _contract.ShouldContain("reduce(CountRecorded event, @Nullable RebuiltTotal current, EventMessage message)");
    [Fact] void should_copy_the_native_body() => _contract.ShouldContain("return new RebuiltTotal(event.id(), (current == null ? 0L : current.amount()) + event.amount());");
    [Fact] void should_copy_the_delete_body() => _contract.ShouldContain("return null;");
    [Fact] void should_delegate_from_an_event_handler() => _projector.ShouldContain("var next = reducer.reduce(event, repository.findById(key).map(RebuiltTotalEntity::toModel).orElse(null), message);");
    [Fact] void should_delete_only_when_the_body_returns_null() => _projector.ShouldContain("if (next == null) repository.deleteById(key); else repository.save(new RebuiltTotalEntity(key, next));");
    [Fact] void should_enable_dependent_specs() => _specs.ShouldNotContain("@Disabled");
    [Fact] void should_exercise_initialization() => _specs.ShouldContain("initializeFromFirstEvent");
    [Fact] void should_exercise_accumulation() => _specs.ShouldContain("accumulateThroughCurrent");
    [Fact] void should_assert_keyed_absence() => _specs.ShouldContain("repository.findById(\"deleted\").map(RebuiltTotalEntity::toModel).stream().findAny().isEmpty()");
    [Fact] void should_use_the_axon_message_resolver() => _specs.ShouldContain("new AnnotationMessageTypeResolver().resolve(event.getClass()).orElseThrow()");
    [Fact] void should_use_the_runtime_test_message_constructor() => _specs.ShouldContain("new GenericEventMessage(type, event)");
    [Fact] void should_confirm_the_actual_event_record_accessors() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CountRecorded.java", StringComparison.Ordinal))).ShouldContain("CounterId id, Long amount");

    [Fact]
    void should_consume_only_placed_transition_bodies()
    {
        var located = ScopeSelection.Select(_request).Single(located => located.Slice.ReadModels.Any(model => model.Name == "RebuiltTotal"));
        foreach (var transition in located.Slice.Reducers.Single().Transitions) _context.ConsumedImplementations.Contains(transition.RequirementId).ShouldBeTrue();
    }

    [Fact]
    void should_ignore_events_for_an_unimplemented_transition_without_creating_or_deleting_state()
    {
        var request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        var contract = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducer.java", StringComparison.Ordinal)));
        contract.ShouldContain("return current;");
        contract.ShouldContain("events are ignored until a supported body is authored");
        contract.ShouldNotContain("throw new");
        var placeholder = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducerPlaceholderTest.java", StringComparison.Ordinal)));
        placeholder.ShouldContain("assertNull(reducer.reduce((CountRecorded) null, null, null)");
        placeholder.ShouldContain("assertSame(current, reducer.reduce((CountRecorded) null, current, null)");
        context.ConsumedImplementations.Count.ShouldEqual(0);
    }

    [Fact]
    void should_refuse_malformed_reducer_shapes_before_emitting_overloads()
    {
        var located = ScopeSelection.Select(_request).Single(located => located.Slice.ReadModels.Any(model => model.Name == "RebuiltTotal"));
        var model = located.Slice.ReadModels.Single();
        var reducer = located.Slice.Reducers.Single();
        SemanticReducer[] malformed =
        [
            reducer with { Target = (SemanticProjectionTargetKind)999 },
            reducer with { ReadModel = located.Slice.Id },
            reducer with { Transitions = [reducer.Transitions[0], reducer.Transitions[0]] },
            reducer with { Transitions = [reducer.Transitions[0] with { EventContract = located.Slice.Id }] }
        ];
        foreach (var candidate in malformed)
        {
            var context = new AxonEmissionContext(_request, new(_request.Profile));
            new ReducerProjection(context, new(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name))).Emit(located, model, candidate);
            context.Artifacts.Count.ShouldEqual(0);
            context.ConsumedImplementations.Count.ShouldEqual(0);
            context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0306").ShouldBeTrue();
        }
    }

    [Fact]
    void should_leave_foreign_and_unplaced_bodies_in_the_audit()
    {
        var foreign = _request.ImplementationRequirements.First(candidate => candidate.Role == SemanticImplementationRole.ReducerTransition);
        var request = _request with { ImplementationRequirements = [.. _request.ImplementationRequirements.Select(candidate => candidate == foreign ? candidate with { Language = "csharp" } : candidate)] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        InlineImplementationDiagnostics.Emit(context);
        context.ConsumedImplementations.Contains(foreign.RequirementId).ShouldBeFalse();
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-IMPL-LANGUAGE").ShouldBeTrue();
    }
}
