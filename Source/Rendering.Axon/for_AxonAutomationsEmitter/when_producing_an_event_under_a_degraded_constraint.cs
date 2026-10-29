// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter.given;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonAutomationsEmitter;

public class when_producing_an_event_under_a_degraded_constraint : given.a_reaction_model
{
    void Render(bool strict)
    {
        Produce();
        var contract = Cratis.Stage.Rendering.Axon.Sdk.ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).Single(candidate => candidate.Id == reaction.Triggers[0].Produces[0].EventContract);
        var constraint = UnsupportedEnumUniqueness.For(contract);
        SemanticFeature Feature(SemanticFeature feature) => feature with { Features = [.. feature.Features.Select(Feature)], Slices = [.. feature.Slices.Select(slice => slice.Events.Any(candidate => candidate.Id == contract.Id) ? slice with { Constraints = [constraint], Specifications = [] } : slice)] };
        var application = request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] };
        request = request with { Model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application), Profile = AxonRendering.CreateProfile(strict: strict) };
        Render();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_refuse_the_production(bool strict)
    {
        Render(strict);
        Code("SubmitOrderReaction").ShouldNotContain("@Component");
        Code("SubmitOrderReaction").ShouldNotContain("SubmitOrderOn0Append");
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/SubmitOrderOn0Append.java", StringComparison.Ordinal)).ShouldBeFalse();
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("SetValidation.java", StringComparison.Ordinal)).ShouldBeFalse();
        var diagnostic = context.Degradation.UnsupportedConstructs.Single(candidate => candidate.Code == AxonRefusalCodes.Constraint);
        diagnostic.Severity.ShouldEqual(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
    }
}
