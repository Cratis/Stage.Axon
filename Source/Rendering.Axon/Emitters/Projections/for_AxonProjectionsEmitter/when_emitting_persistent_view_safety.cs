// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_emitting_persistent_view_safety : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext _application;
    AxonEmissionContext _view;
    AxonEmissionContext _viewless;

    void Because()
    {
        var slices = ScopeSelection.Select(request);
        _application = Emit(request);
        _view = Emit(request with { Scope = new(ArtifactRenderScopeKind.Slice, slices.First(slice => !slice.Slice.ReadModels.IsEmpty).Slice.Id) });
        _viewless = Emit(request with { Scope = new(ArtifactRenderScopeKind.Slice, slices.First(slice => slice.Slice.ReadModels.IsEmpty).Slice.Id) });
    }

    [Fact] void should_have_multiple_views_to_protect() => Configurations(_application).Count().ShouldBeGreaterThan(1);
    [Fact] void should_guard_or_persist_every_selected_view() => Configurations(_application).ShouldEachConformTo(code =>
        code.Contains("public static PersistentReadModelSafety persistentReadModelSafety()", StringComparison.Ordinal) ||
        code.Contains("return new JpaReadModelStore<>", StringComparison.Ordinal));
    [Fact] void should_name_every_guard_uniquely() => Guards(_application).Select(code => code.Split('\n').Single(line => line.Contains(".persistentReadModelSafety\")", StringComparison.Ordinal))).Distinct().Count().ShouldEqual(Guards(_application).Count());
    [Fact] void should_profile_every_configuration() => Configurations(_application).ShouldEachConformTo(code => code.Contains("@Profile(\"persistent\")", StringComparison.Ordinal));
    [Fact] void should_guard_a_selected_view() => Configurations(_view).ShouldNotBeEmpty();
    [Fact] void should_not_emit_view_configurations_for_a_viewless_selected_slice() => Configurations(_viewless).ShouldBeEmpty();
    [Fact] void should_not_emit_a_guard_for_unselected_views() => _viewless.Artifacts.Select(Text).Any(code => code.Contains("persistentReadModelSafety", StringComparison.Ordinal)).ShouldBeFalse();

    static AxonEmissionContext Emit(ArtifactRenderRequest selected)
    {
        var context = new AxonEmissionContext(selected, new(selected.Profile));
        new AxonProjectionsEmitter().Emit(context);
        return context;
    }

    static IEnumerable<string> Configurations(AxonEmissionContext context) => context.Artifacts.Where(artifact => artifact.RelativePath.EndsWith("StoreConfiguration.java", StringComparison.Ordinal)).Select(Text);

    static IEnumerable<string> Guards(AxonEmissionContext context) => Configurations(context).Where(code => code.Contains("persistentReadModelSafety()", StringComparison.Ordinal));

    static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
}
