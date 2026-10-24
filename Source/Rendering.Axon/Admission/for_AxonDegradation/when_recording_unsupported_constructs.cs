// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_recording_unsupported_constructs : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Theory]
    [InlineData(false, ArtifactRenderDiagnosticSeverity.Warning)]
    [InlineData(true, ArtifactRenderDiagnosticSeverity.Error)]
    public void should_record_each_construct_and_apply_strict_severity(bool strict, ArtifactRenderDiagnosticSeverity severity)
    {
        var degradation = new AxonDegradation(AxonRendering.CreateProfile(strict: strict));
        var diagnostic = degradation.Unsupported(AxonRefusalCodes.Implementation, loaded.Model.Application.Id, "Inline bodies are not implemented.");
        diagnostic.Severity.ShouldEqual(severity);
        degradation.UnsupportedConstructs.Single().ShouldEqual(diagnostic);
        diagnostic.Artifact.ShouldEqual(loaded.Model.Application.Id);
        diagnostic.Message.ShouldEqual("Inline bodies are not implemented.");
        degradation.Refusals.Length.ShouldEqual(strict ? 1 : 0);
        if (strict) degradation.Refusals.Single().Codes.ShouldEqual(AxonRefusalCodes.Implementation);
    }
}
