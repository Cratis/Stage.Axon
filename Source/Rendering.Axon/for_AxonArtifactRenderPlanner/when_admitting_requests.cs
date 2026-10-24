// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Serialization;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_admitting_requests : given.an_axon_request
{
    [Fact]
    void should_admit_the_measured_pair() => AxonAdmission.Inspect(request).ShouldBeEmpty();

    [Theory]
    [InlineData(1, 0, 1, 0)]
    [InlineData(2, 1, 2, 0)]
    [InlineData(2, 0, 2, 1)]
    [InlineData(3, 0, 3, 0)]
    [InlineData(2, 0, 3, 0)]
    public void should_refuse_unmeasured_pairs_without_artifacts(int languageMajor, int languageMinor, int semanticMajor, int semanticMinor)
    {
        var model = SemanticModelSerializer.Deserialize(SemanticModelSerializer.Serialize(loaded.Model).AsSpan());
        typeof(ExecutableSemanticModel).GetField("<LanguageVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(model, new LanguageVersion((uint)languageMajor, (uint)languageMinor));
        typeof(ExecutableSemanticModel).GetField("<SemanticVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(model, new SemanticVersion((uint)semanticMajor, (uint)semanticMinor));
        AssertRefused(new AxonArtifactRenderPlanner().Plan(request with { Model = model }), AxonRefusalCodes.Version);
    }

    [Theory]
    [InlineData(ImplementationGapMode.Stub, ArtifactRefusalMode.Refuse, AxonRefusalCodes.GapMode)]
    [InlineData(ImplementationGapMode.Refuse, ArtifactRefusalMode.RenderAround, AxonRefusalCodes.RefusalMode)]
    public void should_refuse_unsupported_modes(ImplementationGapMode gaps, ArtifactRefusalMode refusals, string code) =>
        AssertRefused(AxonRendering.PlanFrom(loaded, request.Scope, profile: AxonRendering.CreateProfile(gaps, refusals)), code);

    [Fact]
    void should_refuse_a_foreign_target() =>
        AssertRefused(new AxonArtifactRenderPlanner().Plan(request with { Profile = ArtifactRenderProfile.Create("cratis", AxonRendering.Version, "stage-axon", AxonRendering.Version, []) }), AxonRefusalCodes.Profile);

    [Fact]
    void should_refuse_unknown_scope_ids() =>
        AssertInvalidScope(request with { Scope = new(ArtifactRenderScopeKind.Slice, loaded.Model.Application.Id) });

    [Fact]
    void should_refuse_combining_application_scope() =>
        AssertInvalidScope(request with { AdditionalScopes = [new(ArtifactRenderScopeKind.Module, loaded.Model.Application.Modules[0].Id)] });

    [Fact]
    void should_refuse_unverified_scaffold_instead_of_silently_succeeding() =>
        AssertRefused(AxonRendering.PlanFrom(loaded, request.Scope), AxonRefusalCodes.Scaffold);

    static void AssertInvalidScope(ArtifactRenderRequest request)
    {
        Assert.Contains(AxonAdmission.Inspect(request), diagnostic => diagnostic.Code == AxonRefusalCodes.Scope && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error);
        Assert.Throws<InvalidArtifactRenderContract>(() => new AxonArtifactRenderPlanner().Plan(request));
    }

    static void AssertRefused(ArtifactRenderPlan plan, string code)
    {
        plan.Success.ShouldBeFalse();
        plan.Artifacts.ShouldBeEmpty();
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error);
    }
}
