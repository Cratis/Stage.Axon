// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.for_UnsupportedSummary;

public class when_rendering_degradations_that_cannot_be_located : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    [Fact]
    void should_render_every_row_and_escape_cells_without_throwing()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        context.Artifacts.Add(PlannedArtifact.CreateText("src/main/java/Empty.java", ""));
        context.Degradation.Unsupported("STAGE-AXON-UNKNOWN", loaded.Model.Application.Id, "reason|one\r\ntwo", modelPath: "");
        context.Degradation.Unsupported("STAGE-AXON-OTHER", loaded.Model.Application.Id, "render the model", "render the model", "external/path");
        Assert.Null(Record.Exception(() => UnsupportedSummary.Emit(context)));
        var text = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan());
        Assert.Equal(2, text.Split('\n').Count(line => line.StartsWith("| STAGE-", StringComparison.Ordinal)));
        Assert.Contains("reason/one  two", text, StringComparison.Ordinal);
        Assert.Contains("(external/path)", text, StringComparison.Ordinal);
    }
}
