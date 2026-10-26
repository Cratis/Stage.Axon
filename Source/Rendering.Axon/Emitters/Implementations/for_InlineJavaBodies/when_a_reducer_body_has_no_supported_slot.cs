// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_a_reducer_body_has_no_supported_slot : a_view_request
{
    [Theory]
    [InlineData("java", false)]
    [InlineData("csharp", false)]
    [InlineData("java", true)]
    [InlineData("csharp", true)]
    public void should_keep_the_body_diagnostic_only_and_the_native_specification_disabled(string language, bool strict)
    {
        const string body = "/* reducer-body-secret */ return current;";
        var requirement = _request.ImplementationRequirements.First(candidate => candidate.Role == SemanticImplementationRole.ReducerTransition) with
        {
            Language = language,
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)))
        };
        var request = _request with
        {
            Profile = AxonRendering.CreateProfile(strict: strict),
            ImplementationRequirements = [requirement],
            ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, body)
        };
        var context = new AxonEmissionContext(request, new(request.Profile));
        InlineImplementationDiagnostics.Emit(context);
        var comment = Text(context.Artifacts.Single());
        comment.ShouldContain("Reducer transition bodies have no supported Axon method-body slot.");
        comment.ShouldContain(requirement.RequirementId);
        comment.ShouldNotContain("reducer-body-secret");
        context.ConsumedImplementations.ShouldBeEmpty();
        new AxonViewSpecsEmitter().Emit(context);
        var spec = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).Single(candidate => candidate.Name == "RebuildFromEvents");
        context.Specifications[spec.Id].DisabledCode.ShouldEqual("STAGE-AXON-0306");
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-0306").ShouldBeTrue();
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
        foreach (var artifact in context.Artifacts) Text(artifact).ShouldNotContain("reducer-body-secret");
    }
}
