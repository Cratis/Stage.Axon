// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_auditing_unplaced_bodies : a_view_request
{
    [Fact]
    void should_preserve_foreign_intent_without_copying_code()
    {
        var context = Audit();
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.ImplementationLanguage).ShouldBeTrue();
        var code = Text(context.Artifacts.Single());
        code.ShouldContain("public final class UnsupportedImplementations");
        foreach (var content in _request.ImplementationContents.Values) code.ShouldNotContain(content);
    }

    [Fact]
    void should_skip_consumed_requirements()
    {
        var context = new AxonEmissionContext(_request, new(_request.Profile));
        foreach (var requirement in _request.ImplementationRequirements) context.ConsumedImplementations.Add(requirement.RequirementId);
        InlineImplementationDiagnostics.Emit(context);
        context.Artifacts.ShouldBeEmpty();
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }

    [Fact]
    void should_ignore_requirements_outside_the_scope()
    {
        var slice = ScopeSelection.Select(_request).First(located => located.Slice.ReadModels.IsEmpty && located.Slice.Reducers.IsEmpty).Slice;
        var request = _request with { Scope = new(ArtifactRenderScopeKind.Slice, slice.Id) };
        var context = new AxonEmissionContext(request, new(request.Profile));
        InlineImplementationDiagnostics.Emit(context);
        context.Artifacts.ShouldBeEmpty();
    }

    [Fact] void should_refuse_in_strict_mode() => Audit(true).Degradation.Refusals.IsEmpty.ShouldBeFalse();

    [Fact]
    void should_warn_without_refusing_by_default()
    {
        var context = Audit();
        context.Degradation.UnsupportedConstructs.Where(diagnostic => diagnostic.Code == AxonRefusalCodes.ImplementationLanguage)
            .All(diagnostic => diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
        context.Degradation.Refusals.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    void should_refuse_the_foreign_language_in_strict_mode() =>
        Audit(true).Degradation.Refusals.Any(refusal => refusal.Codes.Contains(AxonRefusalCodes.ImplementationLanguage)).ShouldBeTrue();

    [Fact] void should_name_the_language_in_the_comment() => Text(Audit().Artifacts.Single()).ShouldContain("language 'csharp'");

    [Fact]
    void should_be_deterministic() => Encoding.UTF8.GetString(Audit().Artifacts.Single().Bytes.AsSpan()).ShouldEqual(Encoding.UTF8.GetString(Audit().Artifacts.Single().Bytes.AsSpan()));

    AxonEmissionContext Audit(bool strict = false)
    {
        var context = new AxonEmissionContext(_request, new(AxonRendering.CreateProfile(strict: strict)));
        InlineImplementationDiagnostics.Emit(context);
        return context;
    }
}
