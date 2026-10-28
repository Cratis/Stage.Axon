// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Emitters;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonArtifactRenderPlanner;

public class when_artifact_paths_collide : given.an_axon_request
{
    [Theory]
    [InlineData("Item")]
    [InlineData("item")]
    void should_keep_the_first_artifact_and_document_dropped_references(string later)
    {
        var plan = new AxonArtifactRenderPlanner([new Colliding(later)]).Plan(request);
        plan.Success.ShouldBeTrue();
        plan.Artifacts.Count(artifact => artifact.RelativePath.EndsWith(".java", StringComparison.Ordinal)).ShouldEqual(1);
        var first = plan.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/Item.java", StringComparison.Ordinal));
        Encoding.UTF8.GetString(first.Bytes.AsSpan()).ShouldContain("first");
        plan.Diagnostics.Single(diagnostic => diagnostic.Code == "STAGE-AXON-GENERATION").Severity.ShouldEqual(ArtifactRenderDiagnosticSeverity.Warning);
        var summary = plan.Artifacts.Single(artifact => artifact.RelativePath == "UNSUPPORTED.md");
        Encoding.UTF8.GetString(summary.Bytes.AsSpan()).ShouldContain("References may not compile");
    }

    sealed class Colliding(string later) : IAxonEmitter
    {
        public bool IsImplemented => true;

        public void Emit(AxonEmissionContext context)
        {
            context.AddJava("example", "Item", "package example; public class Item {} // first");
            context.AddJava("example", later, $"package example; public class {later} {{}} // later");
        }
    }
}
