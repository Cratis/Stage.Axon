// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Reflection;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_scoped_views : given.a_view_request
{
    AxonEmissionContext _context;
    ImmutableHashSet<SemanticId> _unadmitted;
    string _projector;

    void Because()
    {
        _unadmitted = (ImmutableHashSet<SemanticId>)typeof(SemanticExecutionPlan).GetProperty("UnadmittedArtifacts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_request.ExecutionPlan)!;
        _context = new(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(_context);
        _projector = Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("BoardProjectionProjector.java", StringComparison.Ordinal)));
    }

    [Fact] void should_expose_only_the_annotated_handler_signature() => _projector.ShouldNotContain("public void on(");
    [Fact] void should_update_root_properties_fluently() => _projector.ShouldContain("state.with");
    [Fact] void should_pass_explicit_message_arguments_in_specs() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("BoardViewSpecificationsTest.java", StringComparison.Ordinal))).ShouldContain(", null);");
    [Fact] void should_copy_children_into_immutable_lists() => _projector.ShouldContain("List.copyOf(rootChild0)");
    [Fact] void should_preserve_original_deferred_event_values() => _projector.ShouldContain("pending.add(() -> projectCardAdded(event, message))");
    [Fact] void should_backfill_joins() => _projector.ShouldContain("joined.apply(state)");
    [Fact] void should_remove_instances() => _projector.ShouldContain("return new ScopeChange<>(null, true, true)");
    [Fact] void should_inject_java_time_clock() => _projector.ShouldContain("Clock clock");
    [Fact] void should_import_list_helpers() => _projector.ShouldContain("import java.util.List;");
    [Fact] void should_import_atomic_flags() => _projector.ShouldContain("import java.util.concurrent.atomic.AtomicBoolean;");

    [Fact]
    void should_not_inline_axon_or_spring_types()
    {
        _projector.ShouldNotBeEmpty();
        _projector.Split('\n').Any(line => !line.TrimStart().StartsWith("import ", StringComparison.Ordinal) &&
            new[] { "org.axonframework.", "org.springframework.", "jakarta.", "io.cratis." }.Any(prefix => line.Contains(prefix, StringComparison.Ordinal))).ShouldBeFalse();
    }
    [Fact] void should_register_modeled_native_specs() => _context.Specifications.Count.ShouldEqual(ScopeSelection.Select(_request).Sum(slice => slice.Slice.Specifications.Length));

    [Fact]
    void should_match_the_reference_execution_for_every_projected_spec()
    {
        var runner = new SemanticSpecificationRunner();
        var failures = ReferenceSpecifications().Where(specification => !ReferenceUnavailable(specification))
            .Select(specification => runner.Run(_request.ExecutionPlan, specification.Id)).Where(result => !result.Passed)
            .Select(result => string.Join('\n', result.Failures) + "\nActual: " + string.Join("; ", result.Execution.World.ReadModels.SelectMany(model => model.Values).Select(value => value.TargetProperty + "=" + Describe(value.Value))));
        var messages = string.Join('\n', failures);
        Assert.True(messages.Length == 0, messages);
    }

    [Fact]
    void should_exclude_only_the_context_total_spec_from_reference_execution()
    {
        var excluded = ReferenceSpecifications().Where(ReferenceUnavailable).ToArray();
        Assert.Equal(["RetainingContextAndDetails"], excluded.Select(specification => specification.Name));
        Assert.All(excluded, specification => Assert.Contains(specification.ThenReadModels, expected => expected.ReadModel == ReadModelId("ContextTotal")));
    }

    IEnumerable<SemanticSpecification> ReferenceSpecifications() => ScopeSelection.Select(_request)
        .Where(slice => slice.Slice.Reducers.IsEmpty).SelectMany(slice => slice.Slice.Specifications);

    bool ReferenceUnavailable(SemanticSpecification specification) => specification.ThenReadModels.Any(expected =>
        _request.ExecutionPlan.Projections.Values.Any(projection => projection.ReadModel == expected.ReadModel && _unadmitted.Contains(projection.Id)));

    static string Describe(SemanticValue value) => value switch
    {
        SemanticCompositeValue composite => "{" + string.Join(',', composite.Properties.Select(property => property.TargetProperty + "=" + Describe(property.Value))) + "}",
        SemanticArrayValue array => "[" + string.Join(',', array.Values.Select(Describe)) + "]",
        _ => value.ToString()
    };
}
