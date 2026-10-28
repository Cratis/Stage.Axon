// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_selecting_a_file_header : given.an_axon_request
{
    [Fact]
    void should_emit_no_header_by_default()
    {
        var plan = AxonRendering.PlanFrom(loaded, request.Scope);
        plan.Success.ShouldBeTrue();
        plan.Artifacts.Any(IsJava).ShouldBeTrue();
        plan.Artifacts.Where(IsJava).All(artifact => Text(artifact).StartsWith("package ", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    void should_treat_an_empty_header_as_absent() =>
        AxonRendering.CreateProfile(fileHeader: []).Inputs.Select(input => input.Name).SequenceEqual(request.Profile.Inputs.Select(input => input.Name)).ShouldBeTrue();

    [Theory]
    [InlineData(new[] { "Copyright (c) Example" }, "// Copyright (c) Example\n\n")]
    [InlineData(new[] { "Copyright (c) Example", "All rights reserved." }, "// Copyright (c) Example\n// All rights reserved.\n\n")]
    [InlineData(new[] { "Example  ", "", "*/ public class NotCode {}" }, "// Example\n//\n// */ public class NotCode {}\n\n")]
    public void should_prefix_only_java_files_once(string[] lines, string header)
    {
        var baseline = AxonRendering.PlanFrom(loaded, request.Scope);
        var profile = AxonRendering.CreateProfile(fileHeader: [.. lines]);
        var plan = AxonRendering.PlanFrom(loaded, request.Scope, profile: profile);
        plan.Success.ShouldBeTrue();
        plan.Artifacts.Length.ShouldEqual(baseline.Artifacts.Length);
        profile.Inputs.Any(input => input.Name == FileHeaderInput.Name).ShouldBeTrue();
        foreach (var artifact in plan.Artifacts)
        {
            var original = baseline.Artifacts.Single(item => item.RelativePath == artifact.RelativePath);
            Text(artifact).ShouldEqual((IsJava(artifact) ? header : string.Empty) + Text(original));
            artifact.Sources.SequenceEqual(original.Sources).ShouldBeTrue();
        }
        plan.Artifacts.Any(artifact => artifact.RelativePath.Contains("/runtime/", StringComparison.Ordinal)).ShouldBeTrue();
        plan.Artifacts.Any(artifact => artifact.RelativePath.StartsWith("src/test/java/", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("two\rlines")]
    [InlineData("two\\u000alines")]
    [InlineData("two\\uuuu000Dlines")]
    [InlineData(null)]
    public void should_reject_header_lines_that_are_not_single_line_text(string? line) =>
        Assert.Throws<InvalidArtifactRenderContract>(() => AxonRendering.CreateProfile(fileHeader: [line!]));

    [Theory]
    [InlineData("1", "not json")]
    [InlineData("1", "null")]
    [InlineData("1", "[]")]
    [InlineData("1", "[null]")]
    [InlineData("1", "[\"two\\nlines\"]")]
    [InlineData("1", "[\"two\\rlines\"]")]
    [InlineData("1", "[\"two\\\\u000alines\"]")]
    [InlineData("1", "[ \"noncanonical\" ]")]
    [InlineData("2", "[\"Example\"]")]
    public void should_refuse_malformed_header_inputs(string version, string json)
    {
        var input = ArtifactRenderInput.Create(FileHeaderInput.Name, version, [.. Encoding.UTF8.GetBytes(json)]);
        var profile = ArtifactRenderProfile.Create(request.Profile.Target, request.Profile.TargetVersion, request.Profile.Renderer, request.Profile.RendererVersion, request.Profile.Inputs.Add(input));
        var plan = AxonRendering.PlanFrom(loaded, request.Scope, profile: profile);
        plan.Success.ShouldBeFalse();
        plan.Artifacts.ShouldBeEmpty();
        plan.Diagnostics.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.Profile).ShouldBeTrue();
    }

    static bool IsJava(PlannedArtifact artifact) => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal);

    static string Text(PlannedArtifact artifact) => Encoding.UTF8.GetString(artifact.Bytes.AsSpan());
}
