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
    [Fact] void should_load_the_row_through_the_repository() => _projector.ShouldContain("repository.findById(key).map(TotalEntity::toModel)");
    [Fact] void should_save_the_row_through_the_repository() => _projector.ShouldContain("repository.save(new TotalEntity(key, state));");
    [Fact] void should_inject_the_typed_repository() => _projector.ShouldContain("private final TotalRepository repository;");
    [Fact] void should_emit_both_event_handlers() => _projector.Split("@EventHandler", StringSplitOptions.None).Length.ShouldEqual(3);
    [Fact] void should_add_the_event_value() => _projector.ShouldContain("+ event.amount()");
    [Fact] void should_subtract_the_event_value() => _projector.ShouldContain("- event.amount()");
    [Fact] void should_clear_the_optional_property() => _projector.ShouldContain("state = new Total(state.name(), state.amount(), state.count(), null);");
    [Fact] void should_update_the_model_fluently() => _projector.ShouldContain("state = new Total(state.name(), state.amount(), ((state.count() == null ? 0L : state.count()) + 1L), state.note());");
    [Fact] void should_emit_copy_methods() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Total.java", StringComparison.Ordinal))).ShouldContain("public Total withCount(Long value)");
    [Fact] void should_not_emit_the_subscribing_properties() => _context.Artifacts.Exists(artifact => artifact.RelativePath == "src/main/resources/axon-views.properties").ShouldBeFalse();
    [Fact] void should_match_view_writers_by_their_namespace() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ViewProcessingConfiguration.java", StringComparison.Ordinal))).ShouldContain("EventProcessorDefinition.pooledStreamingMatching(\"views\")");
    [Fact] void should_use_the_sequential_policy() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ViewProcessingConfiguration.java", StringComparison.Ordinal))).ShouldContain("configuration.sequencingPolicy(SequentialPolicy.INSTANCE)");
    [Fact] void should_not_select_by_package() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/ViewProcessingConfiguration.java", StringComparison.Ordinal))).ShouldNotContain("getPackageName");
    [Fact] void should_carry_the_view_namespace_on_the_projector() => _projector.ShouldContain("@Namespace(\"views\")");
    [Fact] void should_note_eventual_consistency() => _context.Degradation.Notes.Any(note => note.Contains("STAGE-AXON-0311", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_emit_the_repository() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalRepository.java", StringComparison.Ordinal))).ShouldContain("extends JpaRepository<TotalEntity, String>");
}
