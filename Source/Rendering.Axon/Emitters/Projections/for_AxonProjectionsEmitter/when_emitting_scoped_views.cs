// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_scoped_views : given.a_view_request
{
    AxonEmissionContext _context;
    string _projector;

    void Because()
    {
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        _projector = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("BoardProjectionProjector.java", StringComparison.Ordinal)));
    }

    [Fact] void should_copy_children_into_immutable_lists() => _projector.ShouldContain("java.util.List.copyOf(rootChild0)");
    [Fact] void should_preserve_original_deferred_event_values() => _projector.ShouldContain("pending.add(() -> projectCardAdded(event, message))");
    [Fact] void should_backfill_joins() => _projector.ShouldContain("joined.apply(state)");
    [Fact] void should_remove_instances() => _projector.ShouldContain("return new Change<>(null, true, true)");
    [Fact] void should_inject_java_time_clock() => _projector.ShouldContain("java.time.Clock clock");
    [Fact] void should_register_modeled_native_specs() => _context.Specifications.Count.ShouldEqual(ScopeSelection.Select(_request).Sum(slice => slice.Slice.Specifications.Length));

    [Fact]
    void should_match_the_reference_execution_for_every_authored_spec()
    {
        var runner = new SemanticSpecificationRunner();
        var failures = ScopeSelection.Select(_request).SelectMany(slice => slice.Slice.Specifications)
            .Select(specification => runner.Run(_request.ExecutionPlan, specification.Id)).Where(result => !result.Passed)
            .Select(result => string.Join('\n', result.Failures) + "\nActual: " + string.Join("; ", result.Execution.World.ReadModels.SelectMany(model => model.Values).Select(value => value.TargetProperty + "=" + Describe(value.Value))));
        var messages = string.Join('\n', failures);
        Assert.True(messages.Length == 0, messages);
    }

    static string Describe(SemanticValue value) => value switch
    {
        SemanticCompositeValue composite => "{" + string.Join(',', composite.Properties.Select(property => property.TargetProperty + "=" + Describe(property.Value))) + "}",
        SemanticArrayValue array => "[" + string.Join(',', array.Values.Select(Describe)) + "]",
        _ => value.ToString()
    };
}
