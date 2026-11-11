// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

/// <summary>Stage 5 owns the header and applies it to every text artifact with a known comment syntax, so the generated TypeScript client is now prefixed too (previously only Java files were).</summary>
public class when_a_file_header_is_requested : given.a_client_request
{
    ArtifactRenderPlan _headed;

    void Because()
    {
        Render();
        var profile = AxonRendering.WithTypeScriptClient(AxonRendering.CreateProfile(fileHeader: ["Example"]));
        _headed = new AxonArtifactRenderPlanner().Plan(new(loaded.Model, loaded.Plan, profile, request.Scope)
        {
            ImplementationRequirements = loaded.ImplementationRequirements,
            ImplementationContents = loaded.ImplementationContents
        });
    }

    [Fact] void should_succeed() => _headed.Success.ShouldBeTrue();

    [Fact]
    void should_prefix_every_typescript_client_file_with_the_header()
    {
        var typeScript = _headed.Artifacts.Where(artifact => artifact.RelativePath.StartsWith("client/", StringComparison.Ordinal) && artifact.RelativePath.EndsWith(".ts", StringComparison.Ordinal)).ToArray();
        typeScript.ShouldNotBeEmpty();
        typeScript.All(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).StartsWith("// Example\n\n", StringComparison.Ordinal)).ShouldBeTrue();
    }
}
