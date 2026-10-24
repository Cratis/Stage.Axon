// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Runtime.for_AxonRuntimeEmitter;

public class when_emitting_runtime : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;

    void Because()
    {
        context = new(request, new AxonDegradation(request.Profile));
        new AxonRuntimeEmitter().Emit(context);
    }

    [Fact]
    void should_emit_every_runtime_file_in_the_agreed_package() =>
        context.Artifacts.TrueForAll(artifact => artifact.RelativePath.Contains($"/java/{context.BasePackage}/runtime/", StringComparison.Ordinal)).ShouldBeTrue();

    [Fact]
    void should_emit_primitive_jackson_support_in_its_own_package() =>
        context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/runtime/json/ConceptsModule.java", StringComparison.Ordinal)).RelativePath.ShouldContain("src/main/java/");

    [Fact]
    void should_keep_test_support_out_of_the_application_sources() =>
        context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/TestSupport.java", StringComparison.Ordinal)).RelativePath.ShouldContain("src/test/java/");

    [Fact]
    void should_replace_every_package_placeholder() =>
        context.Artifacts.Exists(artifact => Encoding.UTF8.GetString(artifact.Bytes.AsSpan()).Contains("__RUNTIME__", StringComparison.Ordinal)).ShouldBeFalse();

    [Fact]
    void should_not_emit_a_custom_clock() =>
        context.Artifacts.Exists(artifact => artifact.RelativePath.EndsWith("/Clock.java", StringComparison.Ordinal)).ShouldBeFalse();
}
