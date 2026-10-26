// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters.Projections.for_AxonProjectionsEmitter.given;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Implementations.for_InlineJavaBodies;

public class when_a_body_contains_carriage_returns : a_view_request
{
    [Theory]
    [InlineData("\r", false)]
    [InlineData("\r\n", false)]
    [InlineData("\r", true)]
    [InlineData("\r\n", true)]
    public void should_reject_without_normalizing_or_copying_the_body(string ending, bool strict)
    {
        var body = "/* transport-secret */" + ending + "return java.util.List.of();";
        var seed = _request.ImplementationRequirements.Single(candidate => candidate.Role == SemanticImplementationRole.QueryPerformer);
        var requirement = seed with { Language = "java", ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))) };
        var request = _request with
        {
            Profile = AxonRendering.CreateProfile(strict: strict),
            ImplementationRequirements = [requirement],
            ImplementationContents = _request.ImplementationContents.SetItem(requirement.RequirementId, body)
        };
        var context = new AxonEmissionContext(request, new(request.Profile));
        InlineJavaBodies.TryResolve(context, new(requirement.RequirementId, requirement.Role, requirement.Source.SemanticId), out var resolved, out var reason).ShouldBeFalse();
        resolved.ShouldEqual(string.Empty);
        reason.ShouldContain("LF-only");
        var located = ScopeSelection.Select(request).First(slice => slice.Slice.ReadModels.Any(model => model.Id == requirement.QueryShape.ReadModel));
        QueryPerformer.Emit(context, "sample.queries", QueryPerformer.Queries(context, located).Single(), "sample.queries.Item");
        InlineImplementationDiagnostics.Emit(context);
        var diagnostic = context.Degradation.UnsupportedConstructs.Single(candidate => candidate.Message.Contains("LF-only", StringComparison.Ordinal));
        diagnostic.Artifact.ShouldEqual(requirement.Source.SemanticId);
        diagnostic.Severity.ShouldEqual(strict ? ArtifactRenderDiagnosticSeverity.Error : ArtifactRenderDiagnosticSeverity.Warning);
        context.Degradation.Refusals.IsEmpty.ShouldEqual(!strict);
        context.ConsumedImplementations.ShouldBeEmpty();
        foreach (var artifact in context.Artifacts) Text(artifact).ShouldNotContain("transport-secret");
        Text(context.Artifacts[^1]).ShouldContain("LF-only");
    }
}
