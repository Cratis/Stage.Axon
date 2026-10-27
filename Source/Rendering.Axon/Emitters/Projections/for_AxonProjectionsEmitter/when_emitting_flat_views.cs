// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
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
    [Fact] void should_clear_the_optional_property() => _projector.ShouldContain("state.withNote(null)");
    [Fact] void should_update_the_model_fluently() => _projector.ShouldContain("state.withCount(");
    [Fact] void should_emit_copy_methods() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Total.java", StringComparison.Ordinal))).ShouldContain("public Total withCount(Long value)");
    [Fact] void should_configure_subscribing_processing() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath == "src/main/resources/axon-views.properties")).ShouldContain("axon.eventhandling.processors[axonviews.views.counters.totals].mode=${stage.axon.view-processor-mode:subscribing}");
    [Fact] void should_emit_the_default_profile_guard() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/DefaultViewProcessorGuard.java", StringComparison.Ordinal))).ShouldContain("@Profile(\"!persistent\")");
    [Fact] void should_require_every_generated_namespace() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/DefaultViewProcessorGuard.java", StringComparison.Ordinal))).ShouldContain("\"axonviews.views.counters.totals\"");
    [Fact] void should_inspect_actual_default_processors() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/DefaultViewProcessorGuard.java", StringComparison.Ordinal))).ShouldContain("configuration.getComponents(StreamingEventProcessor.class)");
    [Fact] void should_fail_closed_for_missing_default_processors() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/DefaultViewProcessorGuard.java", StringComparison.Ordinal))).ShouldContain("if (processor == null) throw refusal(name);");
    [Fact] void should_fail_closed_for_streaming_default_processors() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/DefaultViewProcessorGuard.java", StringComparison.Ordinal))).ShouldContain("if (streaming.containsKey(name)) throw refusal(name);");
    [Fact] void should_use_a_model_specific_store_qualifier() => _projector.ShouldContain("@Qualifier(\"axonviews.views.counters.totals.Total\")");
    [Fact] void should_keep_the_qualified_in_memory_store_unchanged() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalStoreConfiguration.java", StringComparison.Ordinal))).ShouldContain("@Bean(\"axonviews.views.counters.totals.Total\")\n    public ReadModelStore<Object, Total> store() {\n        return new InMemoryReadModelStore<>();\n    }");
    [Fact] void should_preserve_the_persistence_difference_in_source() => Text(_context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TotalStoreConfiguration.java", StringComparison.Ordinal))).ShouldContain("// Differs from Screenplay: Default read-model rows are volatile. The persistent profile supports guarded flat projections only; scoped/reducer state is not recovered (STAGE-AXON-0308).");
    [Fact] void should_warn_about_volatile_read_models() => _context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Artifact == ReadModelId("Total")).Code.ShouldEqual("STAGE-AXON-0308");
    [Fact] void should_explain_the_checkpoint_risk() => _context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Artifact == ReadModelId("Total")).Message.ShouldContain("durable checkpoints may skip prior events");
    [Fact] void should_not_promote_the_difference_to_an_error_in_default_mode() => _context.Degradation.UnsupportedConstructs.Single(diagnostic => diagnostic.Artifact == ReadModelId("Total")).Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
}
