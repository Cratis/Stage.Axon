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
    void should_admit_the_measured_pair() => AxonAdmission.Inspect(request, new AxonDegradation(request.Profile)).ShouldBeEmpty();

    [Fact]
    void should_warn_about_unmeasured_pairs() => AssertUnmeasuredPairs(AxonRendering.CreateProfile(), AssertWarned);

    [Fact]
    void should_refuse_unmeasured_pairs_in_strict_mode() => AssertUnmeasuredPairs(AxonRendering.CreateProfile(strict: true), AssertStrictlyRefused);

    [Theory]
    [InlineData(ImplementationGapMode.Stub, ArtifactRefusalMode.Refuse)]
    [InlineData(ImplementationGapMode.Refuse, ArtifactRefusalMode.RenderAround)]
    public void should_warn_for_valid_mode_preferences(ImplementationGapMode gaps, ArtifactRefusalMode refusals) =>
        AssertScaffold(AxonRendering.PlanFrom(loaded, request.Scope, profile: AxonRendering.CreateProfile(gaps, refusals)));

    [Theory]
    [InlineData((ImplementationGapMode)9, ArtifactRefusalMode.Refuse, AxonRefusalCodes.GapMode)]
    [InlineData(ImplementationGapMode.Refuse, (ArtifactRefusalMode)9, AxonRefusalCodes.RefusalMode)]
    public void should_reject_unknown_modes(ImplementationGapMode gaps, ArtifactRefusalMode refusals, string code) =>
        AssertRefused(AxonRendering.PlanFrom(loaded, request.Scope, profile: AxonRendering.CreateProfile(gaps, refusals)), code);

    [Theory]
    [InlineData("invalid", false)]
    [InlineData("false", true)]
    public void should_validate_explicit_strict_inputs(string value, bool valid)
    {
        var input = ArtifactRenderInput.Create("strict", "1", [.. System.Text.Encoding.UTF8.GetBytes(value)]);
        var profile = ArtifactRenderProfile.Create(request.Profile.Target, request.Profile.TargetVersion, request.Profile.Renderer, request.Profile.RendererVersion, request.Profile.Inputs.Add(input));
        var plan = new AxonArtifactRenderPlanner().Plan(request with { Profile = profile });
        if (valid) AssertScaffold(plan);
        else AssertRefused(plan, AxonRefusalCodes.Profile);
    }

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
    void should_emit_the_verified_scaffold() =>
        AssertScaffold(AxonRendering.PlanFrom(loaded, request.Scope));

    [Fact]
    void should_refuse_unimplemented_specifications_in_strict_mode() =>
        AssertStrictlyRefused(AxonRendering.PlanFrom(loaded, request.Scope, profile: AxonRendering.CreateProfile(strict: true)), AxonRefusalCodes.ViewSpecification);

    void AssertUnmeasuredPairs(ArtifactRenderProfile profile, Action<ArtifactRenderPlan, string> assert)
    {
        (LanguageVersion Language, SemanticVersion Semantic)[] pairs =
        [
            (new(1, 0), new(1, 0)),
            (new(2, 1), new(2, 0)),
            (new(2, 0), new(2, 1)),
            (new(3, 0), new(3, 0)),
            (new(2, 0), new(3, 0))
        ];
        foreach (var (language, semantic) in pairs)
        {
            var model = SemanticModelSerializer.Deserialize(SemanticModelSerializer.Serialize(loaded.Model).AsSpan());
            typeof(ExecutableSemanticModel).GetField("<LanguageVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, language);
            typeof(ExecutableSemanticModel).GetField("<SemanticVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, semantic);
            assert(new AxonArtifactRenderPlanner().Plan(request with { Model = model, Profile = profile }), AxonRefusalCodes.Version);
        }
    }

    static void AssertInvalidScope(ArtifactRenderRequest request)
    {
        Assert.Contains(AxonAdmission.Inspect(request, new AxonDegradation(request.Profile)), diagnostic => diagnostic.Code == AxonRefusalCodes.Scope && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error);
        Assert.Throws<InvalidArtifactRenderContract>(() => new AxonArtifactRenderPlanner().Plan(request));
    }

    static void AssertScaffold(ArtifactRenderPlan plan)
    {
        plan.Success.ShouldBeTrue();
        plan.Refusals.ShouldBeEmpty();
        Assert.Contains(plan.Artifacts, artifact => artifact.RelativePath == "pom.xml");
        Assert.DoesNotContain(plan.Diagnostics, diagnostic => diagnostic.Code == AxonRefusalCodes.Scaffold);
    }

    static void AssertWarned(ArtifactRenderPlan plan, string code)
    {
        plan.Success.ShouldBeTrue();
        plan.Refusals.ShouldBeEmpty();
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning);
    }

    static void AssertStrictlyRefused(ArtifactRenderPlan plan, string code)
    {
        AssertRefused(plan, code);
        Assert.Contains(plan.Refusals, refusal => refusal.Codes.Contains(code));
    }

    static void AssertRefused(ArtifactRenderPlan plan, string code)
    {
        plan.Success.ShouldBeFalse();
        plan.Artifacts.ShouldBeEmpty();
        Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.Code == code && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Error);
    }
}
