// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Serialization;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_an_unavailable_reducer_is_planned : Emitters.Projections.for_AxonProjectionsEmitter.given.a_view_request
{
    ArtifactRenderPlan _plan;
    ArtifactRenderPlan _strict;

    void Because()
    {
        var reducer = ScopeSelection.Select(_request).Single(owner => !owner.Slice.Reducers.IsEmpty).Slice.Reducers.Single();
        var model = SemanticModelSerializer.Deserialize(SemanticModelSerializer.Serialize(_request.Model).AsSpan());
        typeof(ExecutableSemanticModel).GetField("<LanguageVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, new LanguageVersion(25, 0));
        typeof(ExecutableSemanticModel).GetField("<SemanticVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, new SemanticVersion(25, 0));
        var request = _request with
        {
            Model = model,
            ImplementationContents = _request.ImplementationContents.Remove(reducer.Transitions[1].RequirementId)
        };
        _plan = new AxonArtifactRenderPlanner().Plan(request);
        _strict = new AxonArtifactRenderPlanner().Plan(request with { Profile = AxonRendering.CreateProfile(strict: true) });
    }

    [Fact]
    void should_continue_with_a_warning_and_an_unsupported_summary()
    {
        _plan.Success.ShouldBeTrue();
        _plan.Refusals.ShouldBeEmpty();
        Assert.Contains(_plan.Diagnostics, diagnostic => diagnostic.Code == "STAGE-AXON-0306" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning);
        Text(_plan.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md")).ShouldContain("STAGE-AXON-0306");
    }

    [Fact]
    void should_not_register_a_partial_reducer_projector() =>
        Assert.DoesNotContain(_plan.Artifacts, artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducerProjector.java", StringComparison.Ordinal));

    [Fact]
    void should_not_emit_a_successful_unchanged_state_fallback() =>
        string.Join('\n', _plan.Artifacts.Where(artifact => artifact.RelativePath.StartsWith("src/main/java/", StringComparison.Ordinal)).Select(Text)).ShouldNotContain("return current;");

    [Fact]
    void should_not_generate_successful_view_tests_that_instantiate_the_quarantined_writer() =>
        string.Join('\n', _plan.Artifacts.Where(artifact => artifact.RelativePath.StartsWith("src/test/java/", StringComparison.Ordinal)).Select(Text)).ShouldNotContain("new RebuiltTotalReducerProjector(");

    [Fact]
    void should_refuse_strict_rendering_without_any_artifacts()
    {
        _strict.Success.ShouldBeFalse();
        _strict.Refusals.IsEmpty.ShouldBeFalse();
        _strict.Artifacts.ShouldBeEmpty();
        Assert.Contains(_strict.Diagnostics, diagnostic => diagnostic.Code == "STAGE-AXON-0306" && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error);
    }
}
