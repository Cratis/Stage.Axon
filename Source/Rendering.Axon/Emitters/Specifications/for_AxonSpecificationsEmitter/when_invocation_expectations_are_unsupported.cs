// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonSpecificationsEmitter;

public class when_invocation_expectations_are_unsupported : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_warn_with_the_specification_identity_and_refuse_when_strict(bool strict)
    {
        var specification = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).First();
        var command = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Commands).First();
        specification = specification with { ThenInvocations = [new(command.Id, [])] };
        var scoped = request with { Profile = AxonRendering.CreateProfile(strict: strict) };
        var context = new AxonEmissionContext(scoped, new(scoped.Profile));
        AxonSpecificationsEmitter.WarnUnverifiedInvocations(context, [specification]);
        var diagnostic = context.Degradation.UnsupportedConstructs.Single();
        diagnostic.Artifact.ShouldEqual(specification.Id);
        diagnostic.Code.ShouldEqual("STAGE-AXON-SPEC");
        diagnostic.Message.ShouldContain("ThenInvocations");
        diagnostic.Severity.ShouldEqual(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
    }

    [Theory]
    [InlineData("SemanticSpecification.ThenInvocations")]
    [InlineData("SemanticSpecificationInvocation.Command")]
    [InlineData("SemanticSpecificationInvocation.Values")]
    public void should_classify_expected_invocations_as_warned(string member)
    {
        SemanticSurfaceLedger.Entries[member].Kind.ShouldEqual(SemanticSurfaceDispositionKind.Warned);
        SemanticSurfaceLedger.Entries[member].Detail.ShouldContain("STAGE-AXON-SPEC");
    }

    [Fact]
    void should_not_warn_for_an_empty_invocation_expectation()
    {
        var specification = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).First() with { ThenInvocations = [] };
        var context = new AxonEmissionContext(request, new(request.Profile));
        AxonSpecificationsEmitter.WarnUnverifiedInvocations(context, [specification]);
        context.Degradation.UnsupportedConstructs.ShouldBeEmpty();
    }
}
