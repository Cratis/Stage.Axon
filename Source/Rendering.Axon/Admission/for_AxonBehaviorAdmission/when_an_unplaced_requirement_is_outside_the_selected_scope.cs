// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonBehaviorAdmission;

public class when_an_unplaced_requirement_is_outside_the_selected_scope : given.a_model_with_behaviors
{
    ArtifactRenderRequestScope inScope;
    ArtifactRenderRequestScope outOfScope;

    void Establish()
    {
        var located = ScopeSelection.Select(request).First(candidate => !candidate.Slice.Commands.IsDefaultOrEmpty);
        var command = located.Slice.Commands[0];
        var sliceAddress = SemanticAddress.ForSlice(ApplicationIdentity.Create(request.Model.Application.Name), located.Module.Name, [.. located.Features.Select(feature => feature.Name)], located.Slice.Name);
        var requirement = new SemanticImplementationRequirement(SemanticImplementationRole.RulePredicate, SemanticAddress.ForCommand(sliceAddress, command.Name), null, "java", null, "hash", new(command.Id, default, default)) { RequirementId = "rule", RequiredCapability = "provider-defined" };
        var withRequirement = request with { ImplementationRequirements = [requirement] };
        inScope = new(withRequirement with { Scope = new(ArtifactRenderScopeKind.Slice, located.Slice.Id) });
        outOfScope = new(withRequirement with { Scope = new(ArtifactRenderScopeKind.Slice, ScopeSelection.Select(request).First(candidate => candidate.Slice.Id != located.Slice.Id).Slice.Id) });
    }

    [Fact] void should_report_it_when_its_slice_is_selected() => AxonBehaviorAdmission.Classify(inScope.Request).Count(decision => decision.Code == AxonRefusalCodes.Implementation).ShouldEqual(1);
    [Fact] void should_not_report_it_for_another_slice() => AxonBehaviorAdmission.Classify(outOfScope.Request).ShouldBeEmpty();

    sealed record ArtifactRenderRequestScope(Cratis.Stage.Contracts.Rendering.ArtifactRenderRequest Request);
}
