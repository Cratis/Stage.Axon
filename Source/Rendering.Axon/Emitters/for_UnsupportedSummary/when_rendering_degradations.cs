// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.for_UnsupportedSummary;

public class when_rendering_degradations : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string summary;
    string location;
    readonly SemanticId unknown = SemanticId.Create(SemanticKind.ReadModel, "Missing.From.The.Application");

    void Because()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        var located = ScopeSelection.Select(request).First(item => item.Slice.ReadModels.Length > 0);
        var model = located.Slice.ReadModels[0];
        var other = ScopeSelection.Select(request).SelectMany(item => item.Slice.ReadModels).First(item => item.Id != model.Id);
        location = string.Join(" / ", located.Path.Where((part, index) => index == 0 || !part.Equals(located.Path[index - 1], StringComparison.OrdinalIgnoreCase))) + " / " + model.Name + " read model";
        context.AddJava("sample", "XProjectorTest", "", true, model.Id);
        context.AddJava("sample", "XProjector", "", false, model.Id);
        context.Degradation.Unsupported(AxonRefusalCodes.ReadModelPersistence, model.Id, "Rows are volatile.");
        context.Degradation.Unsupported(AxonRefusalCodes.ReadModelPersistence, other.Id, "Rows are volatile.");
        context.Degradation.Unsupported("STAGE-AXON-TYPE", model.Id, "No member type.", "represent the requested member", model.Id + "/members/x");
        context.Degradation.Unsupported("STAGE-AXON-UNKNOWN", unknown, "An unknown construct.");
        context.Degradation.Note("Review the model with its authors.");
        UnsupportedSummary.Emit(context);
        summary = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md").Bytes.AsSpan());
    }

    [Fact]
    void should_summarize_distinct_rows_before_the_details()
    {
        var description = Degradations.Find(AxonRefusalCodes.ReadModelPersistence);
        Assert.Contains($"- `STAGE-AXON-0308` (2): {description.Construct}. See {description.AxonDocs}", summary, StringComparison.Ordinal);
        Assert.True(summary.IndexOf("## Summary", StringComparison.Ordinal) < summary.IndexOf("| Code |", StringComparison.Ordinal));
        Assert.DoesNotContain("Rows are volatile.", summary[..summary.IndexOf("## Details", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact] void should_resolve_the_readable_location() => Assert.Contains($"| {location} |", summary, StringComparison.Ordinal);
    [Fact] void should_preserve_member_details() => Assert.Contains($"| {location} (members/x) |", summary, StringComparison.Ordinal);
    [Fact] void should_prefer_the_main_java_artifact() => Assert.Contains("| `src/main/java/sample/XProjector.java` |", summary, StringComparison.Ordinal);
    [Fact] void should_fall_back_to_the_unknown_model_id() => Assert.Contains($"| {unknown} | `—` |", summary, StringComparison.Ordinal);
    [Fact] void should_include_free_form_notes() => Assert.Contains("## Notes\n\nReview the model with its authors.", summary, StringComparison.Ordinal);

    [Fact]
    void should_never_repeat_the_reason_as_intent()
    {
        foreach (var row in summary.Split('\n').Where(line => line.StartsWith("| STAGE-", StringComparison.Ordinal)))
        {
            var cells = row.Split('|');
            Assert.NotEqual(cells[4].Trim(), cells[5].Trim());
        }
        Assert.Contains($"| {Degradations.Find(AxonRefusalCodes.ReadModelPersistence).Construct} | Rows are volatile. |", summary, StringComparison.Ordinal);
    }

    [Fact]
    void should_omit_the_table_when_there_are_no_degradations()
    {
        var context = new AxonEmissionContext(request, new(request.Profile));
        UnsupportedSummary.Emit(context);
        var text = Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
        Assert.Contains("No unsupported constructs.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("| Code |", text, StringComparison.Ordinal);
    }
}
