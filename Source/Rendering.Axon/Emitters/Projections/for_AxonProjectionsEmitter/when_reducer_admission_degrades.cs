// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter;

public class when_reducer_admission_degrades : given.a_view_request
{
    [Theory]
    [InlineData("foreign")]
    [InlineData("file")]
    [InlineData("stale")]
    [InlineData("revision")]
    [InlineData("capability")]
    void should_quarantine_the_whole_model_for_each_unplaced_transition(string cause)
    {
        var request = _request with
        {
            ImplementationRequirements = [.. _request.ImplementationRequirements.Select(requirement => UnplacedRequirement(requirement, cause))]
        };
        var located = ScopeSelection.Select(request).Single(slice => !slice.Slice.Reducers.IsEmpty);
        var context = new AxonEmissionContext(request, new(request.Profile));
        new AxonProjectionsEmitter().Emit(context);
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalReducerProjector.java", StringComparison.Ordinal)).ShouldBeFalse();
        var java = Text(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalStoreConfiguration.java", StringComparison.Ordinal)));
        java.ShouldContain("throw new IllegalStateException");
        java.ShouldNotContain("return current;");
        java.ShouldNotContain("InMemoryReadModelStore");
        context.ConsumedImplementations.Count.ShouldEqual(0);
        var diagnostics = context.Degradation.UnsupportedConstructs.Where(diagnostic => diagnostic.Code == "STAGE-AXON-0306").ToArray();
        diagnostics.Length.ShouldEqual(1);
        diagnostics.All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
        var specs = new AxonEmissionContext(request, new(request.Profile));
        new AxonViewSpecsEmitter().Emit(specs);
        foreach (var spec in located.Slice.Specifications) specs.Specifications[spec.Id].DisabledCode.ShouldBeNull();
        var proof = Text(specs.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/RebuiltTotalQuarantineSpecificationsTest.java", StringComparison.Ordinal)));
        proof.ShouldContain("assertThrows");
        proof.ShouldNotContain("@Disabled");
        proof.ShouldNotContain("new RebuiltTotalReducerProjector(");
    }

    static SemanticImplementationRequirement UnplacedRequirement(SemanticImplementationRequirement requirement, string cause)
    {
        if (requirement.Role != SemanticImplementationRole.ReducerTransition) return requirement;
        return cause switch
        {
            "foreign" => requirement with { Language = "csharp" },
            "file" => requirement with { File = "reducer.java" },
            "stale" => requirement with { ContentHash = "stale" },
            "revision" => requirement with { ContextVersion = 2 },
            _ => requirement with { RequiredCapability = "provider-defined" }
        };
    }

    [Fact]
    void should_leave_every_filtered_transition_unplaced_instead_of_ignoring_the_filter()
    {
        var located = ScopeSelection.Select(_request).Single(slice => !slice.Slice.Reducers.IsEmpty);
        var reducer = located.Slice.Reducers.Single() with { From = new(_request.Model.Application.Id) };
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        var admission = ReducerBodies.Admission(context, located, reducer);
        admission.Blocked.ShouldBeNull();
        admission.Transitions.Values.All(transition => !transition.Placed && transition.Reason.Contains("filter", StringComparison.Ordinal)).ShouldBeTrue();
        new ReducerProjection(context, new(_request.Model.Application, Java.JavaNames.PackageSegment(_request.Model.Application.Name))).Emit(located, located.Slice.ReadModels.Single(), reducer);
        context.Artifacts.ShouldBeEmpty();
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0306").ShouldBeTrue();
        context.ConsumedImplementations.Count.ShouldEqual(0);
    }

    [Fact]
    void should_refuse_strictly_without_changing_the_safe_java()
    {
        var request = _request with { ImplementationContents = _request.ImplementationContents.Clear() };
        var located = ScopeSelection.Select(request).Single(slice => !slice.Slice.Reducers.IsEmpty);
        var warning = new AxonEmissionContext(request, new(request.Profile));
        var strictRequest = request with { Profile = AxonRendering.CreateProfile(strict: true) };
        var strict = new AxonEmissionContext(strictRequest, new(strictRequest.Profile));
        new ReducerProjection(warning, new(request.Model.Application, Java.JavaNames.PackageSegment(request.Model.Application.Name))).Emit(located, located.Slice.ReadModels.Single(), located.Slice.Reducers.Single());
        new ReducerProjection(strict, new(request.Model.Application, Java.JavaNames.PackageSegment(request.Model.Application.Name))).Emit(located, located.Slice.ReadModels.Single(), located.Slice.Reducers.Single());
        strict.Degradation.Refusals.IsEmpty.ShouldBeFalse();
        strict.Degradation.UnsupportedConstructs.All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error).ShouldBeTrue();
        string.Join('\n', strict.Artifacts.Select(Text)).ShouldEqual(string.Join('\n', warning.Artifacts.Select(Text)));
    }

    [Fact]
    void should_repeat_identical_artifacts_and_diagnostics()
    {
        var first = new AxonEmissionContext(_request, new(_request.Profile));
        var second = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonProjectionsEmitter().Emit(first);
        new AxonProjectionsEmitter().Emit(second);
        string.Join('\n', first.Artifacts.Select(artifact => artifact.RelativePath + Text(artifact))).ShouldEqual(string.Join('\n', second.Artifacts.Select(artifact => artifact.RelativePath + Text(artifact))));
        first.Degradation.UnsupportedConstructs.SequenceEqual(second.Degradation.UnsupportedConstructs).ShouldBeTrue();
        var standalone = new AxonEmissionContext(_request, new(_request.Profile));
        new AxonViewSpecsEmitter().Emit(standalone);
        foreach (var spec in first.Specifications) standalone.Specifications[spec.Key].DisabledCode.ShouldEqual(spec.Value.DisabledCode);
    }
}
