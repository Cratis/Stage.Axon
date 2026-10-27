// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.when_planning_through_the_facade;

public class with_bearer_tokens : given.a_facade_model
{
    ArtifactRenderPlan _actual;
    ArtifactRenderPlan _expected;
    ArtifactRenderPlan _default;

    void Establish()
    {
        _expected = AxonRendering.PlanFrom(loaded, scope, profile: AxonRendering.WithBearerTokens(AxonRendering.CreateProfile()));
        _default = AxonRendering.Plan(Request(new()));
    }

    void Because() => _actual = AxonRendering.Plan(Request(new() { HttpAuthentication = AxonHttpAuthentication.BearerTokens }));

    [Fact] void should_keep_artifact_bytes_and_order() => EqualArtifacts(_actual, _expected);
    [Fact]
    void should_keep_diagnostics() => _actual.Diagnostics
        .Select(diagnostic => (diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Artifact))
        .ShouldEqual(_expected.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Artifact)));
    [Fact] void should_keep_the_digest() => _actual.Digest.ShouldEqual(_expected.Digest);

    [Fact]
    void should_differ_from_default_artifacts() => _actual.Artifacts
        .Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan())))
        .SequenceEqual(_default.Artifacts.Select(artifact => (artifact.RelativePath, Bytes: Convert.ToHexString(artifact.Bytes.AsSpan())))).ShouldBeFalse();

    [Fact] void should_differ_from_the_default_digest() => _actual.Digest.ShouldNotEqual(_default.Digest);

    [Fact]
    void should_reject_undefined_authentication() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        AxonRendering.Plan(Request(new() { HttpAuthentication = (AxonHttpAuthentication)2 })));
}
