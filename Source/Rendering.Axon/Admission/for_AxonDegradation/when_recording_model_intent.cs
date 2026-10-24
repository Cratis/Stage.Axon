// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Admission.for_AxonDegradation;

public class when_recording_model_intent : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonDegradation degradation;

    void Because()
    {
        degradation = new(request.Profile);
        degradation.Unsupported("STAGE-AXON-CAPTURE", loaded.Model.Application.Id, "Captures are not implemented.", "capture invoice lines", "invoice.play/capture");
    }

    [Fact]
    void should_preserve_the_intent_and_model_path()
    {
        degradation.Intents.Single().Intent.ShouldEqual("capture invoice lines");
        degradation.Intents.Single().ModelPath.ShouldEqual("invoice.play/capture");
        degradation.Intents.Single().Construct.ShouldEqual(loaded.Model.Application.Id);
    }

    [Fact]
    void should_keep_the_original_diagnostic_message_and_warning_severity()
    {
        degradation.UnsupportedConstructs.Single().Message.ShouldEqual("Captures are not implemented.");
        degradation.UnsupportedConstructs.Single().Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        degradation.Refusals.ShouldBeEmpty();
    }
}
