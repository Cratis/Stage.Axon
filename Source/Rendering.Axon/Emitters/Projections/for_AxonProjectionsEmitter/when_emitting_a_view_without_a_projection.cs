// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Contracts.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_a_view_without_a_projection : Specification
{
    AxonEmissionContext context;

    async Task Establish()
    {
        var root = Path.Combine(for_AxonArtifactRenderPlanner.given.blueprint_models.Root(), "Corpus/live-navigation/model");
        var compilation = await SemanticModelLoader.LoadAsync(root, [], null, "Navigation");
        compilation.Success.ShouldBeTrue();
        var loaded = compilation.Loaded;
        var request = new ArtifactRenderRequest(loaded.Model, loaded.Plan, AxonRendering.CreateProfile(), new(ArtifactRenderScopeKind.Application, loaded.Model.Application.Id));
        context = new(request, new AxonDegradation(request.Profile));
    }

    void Because() => new AxonProjectionsEmitter().Emit(context);

    IEnumerable<string> Configurations => context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).Select(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()));

    [Fact] void should_not_guard_model_only_configurations() => Configurations.ShouldEachConformTo(configuration => !configuration.Contains("persistentReadModelSafety", StringComparison.Ordinal) && !configuration.Contains("@Profile", StringComparison.Ordinal));
    [Fact] void should_keep_an_empty_memory_store() => Configurations.ShouldEachConformTo(configuration => configuration.Contains("return new InMemoryReadModelStore<>();", StringComparison.Ordinal) && configuration.Contains("STAGE-AXON-0309", StringComparison.Ordinal));
    [Fact] void should_warn_once_for_each_writerless_model() => context.Degradation.UnsupportedConstructs.Count.ShouldEqual(4);
    [Fact] void should_use_only_the_writerless_warning() => context.Degradation.UnsupportedConstructs.ShouldEachConformTo(diagnostic => diagnostic.Code == "STAGE-AXON-0309" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning);
    [Fact] void should_explain_why_persistent_startup_is_safe() => context.Degradation.UnsupportedConstructs.ShouldEachConformTo(diagnostic => diagnostic.Message.Contains("always reads empty", StringComparison.Ordinal) && diagnostic.Message.Contains("does not block persistent startup", StringComparison.Ordinal));
    [Fact] void should_have_model_only_configurations_to_guard() => context.Artifacts.Count(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).ShouldBeGreaterThan(0);
    [Fact] void should_emit_the_property_source_required_by_the_store_configuration() => context.Artifacts.Exists(artifact => artifact.RelativePath == "src/main/resources/axon-views.properties").ShouldBeTrue();
}
